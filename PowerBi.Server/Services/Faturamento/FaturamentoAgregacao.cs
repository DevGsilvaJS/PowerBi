using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PowerBi.Server.DTOs;

namespace PowerBi.Server.Services.Faturamento;

/// <summary>
/// Regras de negócio do painel Faturamento (espelha a lógica que existia no Angular).
/// Classe estática pura: fácil de testar unitariamente.
/// </summary>
public static class FaturamentoAgregacao
{
    private const char SepVenda = '\u001f';
    private const int BucketsBarras = 7;

    public static FaturamentoPainelResponse Calcular(
        IReadOnlyList<ProdutoPorOsItem> rows,
        IReadOnlyList<VendaResumoFormaPagamentoItem> formasRaw,
        IReadOnlyDictionary<string, string>? codigoParaCategoriaCadastro,
        IReadOnlyList<VendaFormaPagamentoResumoItemDto> vendaFormaPagamentoResumo)
    {
        var codigoMap =
            codigoParaCategoriaCadastro ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var porTipoValor = new Dictionary<string, double>(StringComparer.Ordinal);
        var porTipoVendasDistintas = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var formasAgg = AgregarFormas(formasRaw);
        var totalFormas = formasAgg.Sum(x => x.Valor);
        var descontoPorForma = AgregarDescontoPonderadoPorPlanoPagamento(rows, vendaFormaPagamentoResumo);

        if (rows.Count == 0)
        {
            return new FaturamentoPainelResponse
            {
                KpiDados = KpisVazios(),
                TotalPagamentoResumo = totalFormas > 0 ? totalFormas : 0,
                FormasPagamento = formasAgg,
                VendasPorMaterialLinhas = new List<VendaMaterialLinhaDto>(),
                VendasPorGrifeSubgrupos = MontarGrifeVazio(),
                VendasPorTipoProdutoLinhas = MontarVendasPorTipoProdutoLinhas(porTipoValor, porTipoVendasDistintas),
                VendasFamiliaProdutoCards = MontarCardsVendasPorFamiliaProdutoVazios(),
                DescontoPorFormaPagamento = descontoPorForma
            };
        }

        var sumPrecoLinhas = 0.0;
        var sumBrutoLinhas = 0.0;
        var sumCusto = 0.0;
        var sumDesconto = 0.0;
        var sumVenReceberLinhas = 0.0;
        var maxPrecoUnitarioLinha = 0.0;

        var liquidoPorVenda = new Dictionary<string, double>(StringComparer.Ordinal);
        var venPorVenda = new Dictionary<string, double>(StringComparer.Ordinal);
        var porVenda = new Dictionary<string, double>(StringComparer.Ordinal);
        var porMaterial = new Dictionary<string, (double Bruto, double Liquido, double Qtd)>(StringComparer.Ordinal);
        var porGrifeArmacoes = new Dictionary<string, (double Bruto, double Liquido, double Qtd)>(StringComparer.Ordinal);
        var porGrifeLentes = new Dictionary<string, (double Bruto, double Liquido, double Qtd)>(StringComparer.Ordinal);
        var porGrifeServicos = new Dictionary<string, (double Bruto, double Liquido, double Qtd)>(StringComparer.Ordinal);
        var porProduto = new Dictionary<string, double>(StringComparer.Ordinal);

        var somaPrecoPorVenda = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var p in rows)
        {
            var pl = ValorLinhaSavWin(p);
            var kAgg = ChaveAgrupamentoVenda(p);
            somaPrecoPorVenda[kAgg] = (somaPrecoPorVenda.GetValueOrDefault(kAgg) + pl);
            var liq = ParseBr(p.ValorLiquidoTotalVenda);
            if (liq > 0)
            {
                liquidoPorVenda[kAgg] = liq;
            }
        }

        var sumLiquidoCabecalho = liquidoPorVenda.Values.Sum();
        var faturamentoUsaLiquidoPorVenda = sumLiquidoCabecalho > 0;

        foreach (var p in rows)
        {
            var precoLinha = ValorLinhaSavWin(p);
            var brutoLinha = ValorBrutoLinhaSavWin(p);
            var liquidoLinha = ValorLiquidoLinhaAlocado(p, somaPrecoPorVenda, liquidoPorVenda, faturamentoUsaLiquidoPorVenda);
            var custo = ParseBr(p.CustoProdutos);
            var desc = ParseBr(p.DescontoValorProduto);
            var qtd = ParseBr(p.QuantidadeTotal);
            var venR = ParseBr(p.VenTotalReceber);

            sumPrecoLinhas += precoLinha;
            sumBrutoLinhas += brutoLinha;
            sumCusto += custo;
            sumDesconto += desc;
            sumVenReceberLinhas += venR;

            var qtdLinha = qtd > 0 ? qtd : 1;
            var unitLiq = liquidoLinha / qtdLinha;
            if (unitLiq > maxPrecoUnitarioLinha)
            {
                maxPrecoUnitarioLinha = unitLiq;
            }

            if (venR > 0)
            {
                venPorVenda[ChaveAgrupamentoVenda(p)] = venR;
            }

            var mat = string.IsNullOrWhiteSpace(p.LinhaDeProduto) ? "—" : p.LinhaDeProduto.Trim();
            if (string.IsNullOrEmpty(mat))
            {
                mat = "—";
            }

            var aggMat = porMaterial.GetValueOrDefault(mat);
            porMaterial[mat] = (aggMat.Bruto + brutoLinha, aggMat.Liquido + liquidoLinha, aggMat.Qtd + qtdLinha);

            var grifeNome = string.IsNullOrWhiteSpace(p.Grife) ? "—" : p.Grife.Trim();
            var sg = SubgrupoPorPrefixoCodigoProduto(p.CodigoProduto);
            if (sg != SubgrupoProduto.Outros)
            {
                var map = sg switch
                {
                    SubgrupoProduto.Armacoes => porGrifeArmacoes,
                    SubgrupoProduto.Lentes => porGrifeLentes,
                    _ => porGrifeServicos
                };
                var aggG = map.GetValueOrDefault(grifeNome);
                map[grifeNome] = (aggG.Bruto + brutoLinha, aggG.Liquido + liquidoLinha, aggG.Qtd + qtdLinha);
            }

            var prod = string.IsNullOrWhiteSpace(p.CodigoProduto)
                ? (string.IsNullOrWhiteSpace(p.FantasiaProduto) ? "—" : p.FantasiaProduto.Trim())
                : p.CodigoProduto.Trim();
            if (string.IsNullOrEmpty(prod))
            {
                prod = "—";
            }

            porProduto[prod] = porProduto.GetValueOrDefault(prod) + precoLinha;
        }

        AgregarVendasPorTipoProdutoPorComposicaoVenda(
            rows,
            somaPrecoPorVenda,
            liquidoPorVenda,
            faturamentoUsaLiquidoPorVenda,
            codigoMap,
            porTipoValor,
            porTipoVendasDistintas);

        var faturamentoTotal = sumLiquidoCabecalho > 0 ? sumLiquidoCabecalho : sumPrecoLinhas;

        var sumVenUnico = venPorVenda.Values.Sum();
        var sumVenReceber = venPorVenda.Count > 0 ? sumVenUnico : sumVenReceberLinhas;

        foreach (var p in rows)
        {
            var kAgg = ChaveAgrupamentoVenda(p);
            liquidoPorVenda.TryGetValue(kAgg, out var liq);
            var precoLinha = ValorLinhaSavWin(p);
            var atual = porVenda.GetValueOrDefault(kAgg);
            if (liq > 0)
            {
                porVenda[kAgg] = liq;
            }
            else
            {
                porVenda[kAgg] = atual + precoLinha;
            }
        }

        var distinctProd = porProduto.Count;
        var margemPct = faturamentoTotal > 0
            ? Math.Min(100, Math.Max(0, (faturamentoTotal - sumCusto) / faturamentoTotal * 100))
            : 0;
        var margemBrutoPct = sumBrutoLinhas > 0
            ? Math.Min(100, Math.Max(0, (sumBrutoLinhas - sumCusto) / sumBrutoLinhas * 100))
            : 0;
        var descontoPctVenda = PercentualDescontoVendaMedio(rows);
        var prodPct = rows.Count > 0 ? Math.Min(100, distinctProd / (double)rows.Count * 100) : 0;

        var qtdOsDistintas = ContarVendasOsDistintas(rows);
        var ticketMedioValor = qtdOsDistintas > 0 ? faturamentoTotal / qtdOsDistintas : 0;
        var ticketPct = maxPrecoUnitarioLinha > 0
            ? Math.Min(100, ticketMedioValor / maxPrecoUnitarioLinha * 100)
            : ticketMedioValor > 0 ? 100 : 0;

        var linhasMaterial = porMaterial
            .Select(kv => (Material: kv.Key, kv.Value.Bruto, kv.Value.Liquido, kv.Value.Qtd))
            .OrderByDescending(x => x.Liquido)
            .ThenBy(x => x.Material, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), false))
            .ToList();

        var totalLiquidoMaterial = linhasMaterial.Sum(x => x.Liquido);
        var vendasPorMaterial = linhasMaterial.Select(l => new VendaMaterialLinhaDto
        {
            Material = l.Material,
            Bruto = l.Bruto,
            Liquido = l.Liquido,
            Quantidade = l.Qtd,
            Percentual = totalLiquidoMaterial > 0 ? l.Liquido / totalLiquidoMaterial * 100 : 0
        }).ToList();

        var vendasPorGrife = MontarVendasPorGrifeSubgrupos(porGrifeArmacoes, porGrifeLentes, porGrifeServicos);

        var barsBruto = BarsFromSeries(rows.Select(ValorBrutoLinhaSavWin).ToList());
        var barsFaturamento = liquidoPorVenda.Count > 0
            ? BarsFromSeries(liquidoPorVenda.Values.ToList())
            : BarsFromSeries(rows.Select(ValorLinhaSavWin).ToList());
        var barsDesc = BarsFromSeries(rows.Select(p => ParseBr(p.DescontoValorProduto)).ToList());
        var barsCusto = BarsFromSeries(rows.Select(p => ParseBr(p.CustoProdutos)).ToList());
        var barsTicket = BarsFromSeries(porVenda.Values.ToList());
        var barsQtd = BarsFromSeries(rows.Select(p => ParseBr(p.QuantidadeTotal) > 0 ? ParseBr(p.QuantidadeTotal) : 1).ToList());

        var totalPagamentoResumo = sumVenReceber > 0 ? sumVenReceber : faturamentoTotal;
        if (totalFormas > 0)
        {
            totalPagamentoResumo = totalFormas;
        }

        var cmvSobreLiquidoPct = faturamentoTotal > 0
            ? Math.Min(100, sumCusto / faturamentoTotal * 100)
            : 0;

        var kpi = new Dictionary<string, KpiDadoDto>(StringComparer.Ordinal)
        {
            ["vendasBruto"] = new KpiDadoDto { Valor = sumBrutoLinhas, Percentual = margemBrutoPct, Bars = barsBruto },
            ["vendas"] = new KpiDadoDto { Valor = faturamentoTotal, Percentual = margemPct, Bars = barsFaturamento },
            ["cmv"] = new KpiDadoDto { Valor = sumCusto, Percentual = cmvSobreLiquidoPct, Bars = barsCusto },
            ["descVendedor"] = new KpiDadoDto { Valor = sumDesconto, Percentual = descontoPctVenda, Bars = barsDesc },
            ["ticketMedio"] = new KpiDadoDto { Valor = ticketMedioValor, Percentual = ticketPct, Bars = barsTicket },
            ["vendasProdutos"] = new KpiDadoDto { Valor = qtdOsDistintas, Percentual = prodPct, Bars = barsQtd }
        };

        return new FaturamentoPainelResponse
        {
            KpiDados = kpi,
            TotalPagamentoResumo = totalPagamentoResumo,
            FormasPagamento = formasAgg,
            VendasPorMaterialLinhas = vendasPorMaterial,
            VendasPorGrifeSubgrupos = vendasPorGrife,
            VendasPorTipoProdutoLinhas = MontarVendasPorTipoProdutoLinhas(porTipoValor, porTipoVendasDistintas),
            VendasFamiliaProdutoCards = MontarCardsVendasPorFamiliaLinha(
                rows,
                somaPrecoPorVenda,
                liquidoPorVenda,
                faturamentoUsaLiquidoPorVenda,
                codigoMap),
            DescontoPorFormaPagamento = descontoPorForma
        };
    }

    /// <summary>Mapeia <c>TIPOPRODUTO</c> do cadastro para o rótulo do card (ordem fixa na UI).</summary>
    public static string MapTipoProdutoParaCategoriaCard(string? tipoProduto)
    {
        if (string.IsNullOrWhiteSpace(tipoProduto))
        {
            return VendasPorTipoProdutoCard.Servicos;
        }

        var t = RemoverDiacriticos(tipoProduto.Trim()).ToUpperInvariant();
        while (t.Contains("  ", StringComparison.Ordinal))
        {
            t = t.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (t.Contains("LENTE", StringComparison.Ordinal) &&
            (t.Contains("E SERV", StringComparison.Ordinal) || t.Contains("E SERVI", StringComparison.Ordinal)))
        {
            return VendasPorTipoProdutoCard.LenteEServico;
        }

        if (t.Contains("SOLAR", StringComparison.Ordinal))
        {
            return VendasPorTipoProdutoCard.Solar;
        }

        if (t.Contains("RECEIT", StringComparison.Ordinal))
        {
            return VendasPorTipoProdutoCard.Receituario;
        }

        if (t.Contains("OCULOS", StringComparison.Ordinal) && t.Contains("COMPLETO", StringComparison.Ordinal))
        {
            return VendasPorTipoProdutoCard.OculosCompleto;
        }

        if (t.Contains("LENTE", StringComparison.Ordinal))
        {
            return VendasPorTipoProdutoCard.Lente;
        }

        if (t.Contains("SERV", StringComparison.Ordinal) || t.Contains("OUTROS", StringComparison.Ordinal))
        {
            return VendasPorTipoProdutoCard.Servicos;
        }

        return VendasPorTipoProdutoCard.Servicos;
    }

    /// <summary>
    /// Índice CODIGO/MATID (<c>ProdutosCadastradosGrid</c>) → categoria derivada de <c>TIPOPRODUTO</c>.
    /// Usado para distinguir Solar vs Receituário em armações; demais famílias seguem prefixo na O.S.
    /// </summary>
    public static Dictionary<string, string> BuildCodigoParaCategoriaMap(
        IReadOnlyList<ProdutosCadastradosGridItem> catalog)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in catalog)
        {
            var cat = MapTipoProdutoParaCategoriaCard(row.TipoProduto);
            var cod = row.Codigo?.Trim();
            if (!string.IsNullOrEmpty(cod))
            {
                RegistrarChavesCodigoProduto(map, cod, cat);
            }

            var mat = row.MatId?.Trim();
            if (!string.IsNullOrEmpty(mat))
            {
                map[mat] = cat;
            }
        }

        return map;
    }

    /// <summary>Armação solar (fallback quando o cadastro não define Solar/Receituário): NCM, descrição, linha.</summary>
    private static bool EhArmacaoSolar(ProdutoPorOsItem p)
    {
        var ncm = (p.NcmCodigo ?? "").Trim();
        if (ncm.Length >= 5 && ncm.StartsWith("90041", StringComparison.Ordinal))
        {
            return true;
        }

        var nd = RemoverDiacriticos((p.NcmDescricao ?? "").Trim()).ToUpperInvariant();
        if (nd.Contains("OCULOS DE SOL", StringComparison.Ordinal) ||
            nd.Contains("OCULO DE SOL", StringComparison.Ordinal))
        {
            return true;
        }

        var dp = RemoverDiacriticos((p.DescricaoProduto ?? "").Trim()).ToUpperInvariant();
        if (dp == "SOLAR" || dp.Contains("OCULOS DE SOL", StringComparison.Ordinal))
        {
            return true;
        }

        var linha = RemoverDiacriticos((p.LinhaDeProduto ?? "").Trim()).ToUpperInvariant();
        return linha.Contains("SOLAR", StringComparison.Ordinal);
    }

    private static bool TryLookupCategoriaPorCodigo(
        IReadOnlyDictionary<string, string> map,
        string? codigoProduto,
        out string categoria)
    {
        categoria = VendasPorTipoProdutoCard.Servicos;
        var c = codigoProduto?.Trim() ?? "";
        if (c.Length == 0)
        {
            return false;
        }

        if (map.TryGetValue(c, out var cat))
        {
            categoria = cat;
            return true;
        }

        var digitos = new string(c.Where(char.IsDigit).ToArray());
        if (digitos.Length > 0 && map.TryGetValue(digitos, out cat))
        {
            categoria = cat;
            return true;
        }

        var semZeros = digitos.TrimStart('0');
        if (semZeros.Length > 0 && map.TryGetValue(semZeros, out cat))
        {
            categoria = cat;
            return true;
        }

        return false;
    }

    private static void RegistrarChavesCodigoProduto(Dictionary<string, string> map, string cod, string categoria)
    {
        map[cod] = categoria;
        var digitos = new string(cod.Where(char.IsDigit).ToArray());
        if (digitos.Length == 0)
        {
            return;
        }

        map[digitos] = categoria;
        var semZeros = digitos.TrimStart('0');
        if (semZeros.Length > 0)
        {
            map[semZeros] = categoria;
        }
    }

    private static void AgregarVendasPorTipoProdutoPorComposicaoVenda(
        IReadOnlyList<ProdutoPorOsItem> rows,
        Dictionary<string, double> somaPrecoPorVenda,
        Dictionary<string, double> liquidoPorVenda,
        bool faturamentoUsaLiquidoPorVenda,
        IReadOnlyDictionary<string, string> codigoParaCategoriaCadastro,
        Dictionary<string, double> porTipoValor,
        Dictionary<string, HashSet<string>> porTipoVendasDistintas)
    {
        foreach (var grp in rows.GroupBy(ChaveAgrupamentoVenda))
        {
            var linhas = grp.ToList();
            double valorVenda = 0;
            foreach (var p in linhas)
            {
                valorVenda += ValorLiquidoLinhaAlocado(p, somaPrecoPorVenda, liquidoPorVenda, faturamentoUsaLiquidoPorVenda);
            }

            var cat = ClassificarVendaPorComposicaoPrefixos(
                linhas,
                codigoParaCategoriaCadastro,
                somaPrecoPorVenda,
                liquidoPorVenda,
                faturamentoUsaLiquidoPorVenda);

            porTipoValor[cat] = porTipoValor.GetValueOrDefault(cat) + valorVenda;

            if (!porTipoVendasDistintas.TryGetValue(cat, out var chaves))
            {
                chaves = new HashSet<string>(StringComparer.Ordinal);
                porTipoVendasDistintas[cat] = chaves;
            }

            chaves.Add(grp.Key);
        }
    }

    private static string ClassificarVendaPorComposicaoPrefixos(
        IReadOnlyList<ProdutoPorOsItem> linhas,
        IReadOnlyDictionary<string, string> codigoParaCategoriaCadastro,
        Dictionary<string, double> somaPrecoPorVenda,
        Dictionary<string, double> liquidoPorVenda,
        bool faturamentoUsaLiquidoPorVenda)
    {
        var has1 = false;
        var has2 = false;
        var has3 = false;
        foreach (var p in linhas)
        {
            switch (SubgrupoPorPrefixoCodigoProduto(p.CodigoProduto))
            {
                case SubgrupoProduto.Armacoes: has1 = true; break;
                case SubgrupoProduto.Lentes: has2 = true; break;
                case SubgrupoProduto.Servicos: has3 = true; break;
                case SubgrupoProduto.Outros: break;
            }
        }

        if (has1 && has2)
        {
            return VendasPorTipoProdutoCard.OculosCompleto;
        }

        if (has2 && has3)
        {
            return VendasPorTipoProdutoCard.LenteEServico;
        }

        if (has3 && !has1 && !has2)
        {
            return VendasPorTipoProdutoCard.Servicos;
        }

        if (has2 && !has1 && !has3)
        {
            return VendasPorTipoProdutoCard.Lente;
        }

        if (has1 && !has2 && !has3)
        {
            return ClassificarTipoArmacaoPonderadoNasLinhas(
                linhas,
                codigoParaCategoriaCadastro,
                somaPrecoPorVenda,
                liquidoPorVenda,
                faturamentoUsaLiquidoPorVenda);
        }

        if (has1 && has3 && !has2)
        {
            var tipoArmacao = ClassificarTipoArmacaoPonderadoNasLinhas(
                linhas,
                codigoParaCategoriaCadastro,
                somaPrecoPorVenda,
                liquidoPorVenda,
                faturamentoUsaLiquidoPorVenda);
            return tipoArmacao == VendasPorTipoProdutoCard.Solar
                ? VendasPorTipoProdutoCard.SolarEServico
                : VendasPorTipoProdutoCard.ReceituarioEServico;
        }

        return VendasPorTipoProdutoCard.Servicos;
    }

    /// <summary>
    /// Só linhas de armação (prefixo 1): Solar vs Receituário pelo cadastro / NCM, ponderado pelo líquido alocado.
    /// </summary>
    private static string ClassificarTipoArmacaoPonderadoNasLinhas(
        IReadOnlyList<ProdutoPorOsItem> linhas,
        IReadOnlyDictionary<string, string> codigoParaCategoriaCadastro,
        Dictionary<string, double> somaPrecoPorVenda,
        Dictionary<string, double> liquidoPorVenda,
        bool faturamentoUsaLiquidoPorVenda)
    {
        double pesoSolar = 0;
        double pesoReceituario = 0;
        foreach (var p in linhas)
        {
            if (SubgrupoPorPrefixoCodigoProduto(p.CodigoProduto) != SubgrupoProduto.Armacoes)
            {
                continue;
            }

            var w = ValorLiquidoLinhaAlocado(p, somaPrecoPorVenda, liquidoPorVenda, faturamentoUsaLiquidoPorVenda);
            var r = ClassificarArmacaoLinhaPorCadastroOuFallback(p, codigoParaCategoriaCadastro);
            if (r == VendasPorTipoProdutoCard.Solar)
            {
                pesoSolar += w;
            }
            else
            {
                pesoReceituario += w;
            }
        }

        if (pesoSolar == 0 && pesoReceituario == 0)
        {
            return VendasPorTipoProdutoCard.Receituario;
        }

        if (pesoSolar > pesoReceituario)
        {
            return VendasPorTipoProdutoCard.Solar;
        }

        if (pesoReceituario > pesoSolar)
        {
            return VendasPorTipoProdutoCard.Receituario;
        }

        return VendasPorTipoProdutoCard.Receituario;
    }

    /// <summary>Confia no cadastro só quando o mapa indica Solar ou Receituário; caso contrário usa NCM/descrição da O.S.</summary>
    private static string ClassificarArmacaoLinhaPorCadastroOuFallback(
        ProdutoPorOsItem p,
        IReadOnlyDictionary<string, string> codigoParaCategoriaCadastro)
    {
        if (TryLookupCategoriaPorCodigo(codigoParaCategoriaCadastro, p.CodigoProduto, out var cat))
        {
            if (cat == VendasPorTipoProdutoCard.Solar || cat == VendasPorTipoProdutoCard.Receituario)
            {
                return cat;
            }
        }

        return EhArmacaoSolar(p) ? VendasPorTipoProdutoCard.Solar : VendasPorTipoProdutoCard.Receituario;
    }

    /// <summary>
    /// Por linha de O.S.: líquido alocado soma ao bucket da família (1 solar/receituário, 2 lentes, 3 serviços);
    /// quantidade = soma de <see cref="ProdutoPorOsItem.QuantidadeTotal"/> por linha (mesmo fallback do KPI: mínimo 1 por linha).
    /// </summary>
    private static List<FaturamentoFamiliaProdutoCardDto> MontarCardsVendasPorFamiliaLinha(
        IReadOnlyList<ProdutoPorOsItem> rows,
        Dictionary<string, double> somaPrecoPorVenda,
        Dictionary<string, double> liquidoPorVenda,
        bool faturamentoUsaLiquidoPorVenda,
        IReadOnlyDictionary<string, string> codigoMap)
    {
        double vSol = 0;
        double vRec = 0;
        double vLen = 0;
        double vSer = 0;
        double qSol = 0;
        double qRec = 0;
        double qLen = 0;
        double qSer = 0;

        foreach (var p in rows)
        {
            var liq = ValorLiquidoLinhaAlocado(p, somaPrecoPorVenda, liquidoPorVenda, faturamentoUsaLiquidoPorVenda);
            var qtd = ParseBr(p.QuantidadeTotal);
            var qtdLinha = qtd > 0 ? qtd : 1;
            switch (SubgrupoPorPrefixoCodigoProduto(p.CodigoProduto))
            {
                case SubgrupoProduto.Armacoes:
                    var tipo = ClassificarArmacaoLinhaPorCadastroOuFallback(p, codigoMap);
                    if (tipo == VendasPorTipoProdutoCard.Solar)
                    {
                        vSol += liq;
                        qSol += qtdLinha;
                    }
                    else
                    {
                        vRec += liq;
                        qRec += qtdLinha;
                    }

                    break;
                case SubgrupoProduto.Lentes:
                    vLen += liq;
                    qLen += qtdLinha;
                    break;
                case SubgrupoProduto.Servicos:
                    vSer += liq;
                    qSer += qtdLinha;
                    break;
                case SubgrupoProduto.Outros:
                    break;
            }
        }

        return new List<FaturamentoFamiliaProdutoCardDto>
        {
            new()
            {
                Id = "solares",
                Titulo = "Solares Vendidos",
                Valor = vSol,
                QuantidadeProdutos = (int)Math.Round(qSol)
            },
            new()
            {
                Id = "receituarios",
                Titulo = "Receituários Vendidos",
                Valor = vRec,
                QuantidadeProdutos = (int)Math.Round(qRec)
            },
            new()
            {
                Id = "lentes",
                Titulo = "Lentes Vendidas",
                Valor = vLen,
                QuantidadeProdutos = (int)Math.Round(qLen)
            },
            new()
            {
                Id = "servicos",
                Titulo = "Serviços Vendidos",
                Valor = vSer,
                QuantidadeProdutos = (int)Math.Round(qSer)
            }
        };
    }

    private static List<FaturamentoFamiliaProdutoCardDto> MontarCardsVendasPorFamiliaProdutoVazios() =>
        new()
        {
            new FaturamentoFamiliaProdutoCardDto
            {
                Id = "solares",
                Titulo = "Solares Vendidos",
                Valor = 0,
                QuantidadeProdutos = 0
            },
            new FaturamentoFamiliaProdutoCardDto
            {
                Id = "receituarios",
                Titulo = "Receituários Vendidos",
                Valor = 0,
                QuantidadeProdutos = 0
            },
            new FaturamentoFamiliaProdutoCardDto
            {
                Id = "lentes",
                Titulo = "Lentes Vendidas",
                Valor = 0,
                QuantidadeProdutos = 0
            },
            new FaturamentoFamiliaProdutoCardDto
            {
                Id = "servicos",
                Titulo = "Serviços Vendidos",
                Valor = 0,
                QuantidadeProdutos = 0
            }
        };

    private static List<VendaTipoProdutoLinhaDto> MontarVendasPorTipoProdutoLinhas(
        Dictionary<string, double> valorPorCategoria,
        Dictionary<string, HashSet<string>> vendasDistintasPorCategoria)
    {
        return VendasPorTipoProdutoCard.Ordem.Select(label =>
        {
            var temChaves = vendasDistintasPorCategoria.TryGetValue(label, out var chaves);
            var chavesOrdenadas = temChaves && chaves != null
                ? chaves.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
            var listaVendas = chavesOrdenadas.Count > 0
                ? chavesOrdenadas
                    .Select(ChaveInternaVendaParaIdentificacao)
                    .OrderBy(v => v.LojaNome, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(v => v.CodigoDaVenda ?? "", StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<VendaPainelIdentificacaoDto>();

            return new VendaTipoProdutoLinhaDto
            {
                Label = label,
                Valor = valorPorCategoria.GetValueOrDefault(label),
                QuantidadeVendas = chavesOrdenadas.Count,
                Vendas = listaVendas,
                VendaChavesInternas = chavesOrdenadas
            };
        }).ToList();
    }

    /// <summary>Converte a chave interna <see cref="ChaveAgrupamentoVenda"/> em DTO (separador <see cref="SepVenda"/>).</summary>
    private static VendaPainelIdentificacaoDto ChaveInternaVendaParaIdentificacao(string chaveInterna)
    {
        var i = chaveInterna.IndexOf(SepVenda, StringComparison.Ordinal);
        if (i < 0)
        {
            return new VendaPainelIdentificacaoDto { LojaNome = chaveInterna, CodigoDaVenda = null };
        }

        var loja = chaveInterna[..i];
        var resto = chaveInterna[(i + 1)..];
        var cod = resto == "__sem_os__" ? null : resto;
        return new VendaPainelIdentificacaoDto
        {
            LojaNome = string.IsNullOrWhiteSpace(loja) ? null : loja,
            CodigoDaVenda = string.IsNullOrWhiteSpace(cod) ? null : cod
        };
    }

    private static string RemoverDiacriticos(string texto)
    {
        var norm = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var ch in norm)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static class VendasPorTipoProdutoCard
    {
        public const string Solar = "Solar";
        public const string Receituario = "Receituário";
        public const string SolarEServico = "Solar + Serviço";
        public const string ReceituarioEServico = "Receituário + Serviço";
        public const string OculosCompleto = "Óculos completo";
        public const string Lente = "Lente";
        public const string LenteEServico = "Lente e serviço";
        public const string Servicos = "Serviços";

        public static readonly string[] Ordem =
        {
            Solar,
            Receituario,
            SolarEServico,
            ReceituarioEServico,
            OculosCompleto,
            Lente,
            LenteEServico,
            Servicos
        };
    }

    private static Dictionary<string, KpiDadoDto> KpisVazios()
    {
        KpiDadoDto Z() => new() { Valor = 0, Percentual = 0, Bars = Enumerable.Repeat(0.0, BucketsBarras).ToList() };
        return new Dictionary<string, KpiDadoDto>(StringComparer.Ordinal)
        {
            ["vendasBruto"] = Z(),
            ["vendas"] = Z(),
            ["cmv"] = Z(),
            ["descVendedor"] = Z(),
            ["ticketMedio"] = Z(),
            ["vendasProdutos"] = Z()
        };
    }

    private static List<VendaGrifeSubgrupoDto> MontarGrifeVazio() =>
        new()
        {
            new VendaGrifeSubgrupoDto { Titulo = "Armações", Linhas = new List<VendaGrifeLinhaDto>() },
            new VendaGrifeSubgrupoDto { Titulo = "Lentes", Linhas = new List<VendaGrifeLinhaDto>() },
            new VendaGrifeSubgrupoDto { Titulo = "Serviços", Linhas = new List<VendaGrifeLinhaDto>() }
        };

    /// <summary>
    /// Desconto da venda, por O.S. + loja em Produtos por O.S.: <c>Σ(ValorBruto)</c> de cada linha (via
    /// <see cref="ValorBrutoLinhaSavWin"/>) menos o <c>ValorLiquidoTotalVenda</c> da venda quando preenchido; se o líquido
    /// vier <c>0</c> em todas as linhas, <c>Σ(ValorBruto) − Σ(PrecoTotal)</c> (desconto catálogo → preço final por linha).
    /// Rateio no resumo por <c>ValorBruto / SUM(ValorBruto)</c>; depois agrega por <c>PlanoPagamento</c>.
    /// Cruzamento resumo × produtos pela chave <see cref="ChaveDescontoVendaLojaOs"/> / <see cref="ChaveGrupoResumoPorLojaNumeroPedido"/>.
    /// Sem match (ex.: linha sem <c>NumeroPedido</c>) usa desconto implícito <c>Σ(ValorBruto − ValorLiquido)</c> no grupo.
    /// <para>
    /// <strong>Quantidade de vendas:</strong> agrupa-se o resumo por loja + <c>NumeroPedido</c> (normalizado); por plano,
    /// quantas chaves distintas; na linha TOTAL, pedidos distintos no resumo (bruto &gt; 0 no grupo).
    /// </para>
    /// </summary>
    private static List<DescontoFormaPagamentoLinhaDto> AgregarDescontoPonderadoPorPlanoPagamento(
        IReadOnlyList<ProdutoPorOsItem> produtosPorOs,
        IReadOnlyList<VendaFormaPagamentoResumoItemDto> resumoRows)
    {
        var descontoPorPedido = DescontoTotalPorVendaBrutoMenosLiquidoTotal(produtosPorOs);
        var porPlano = new Dictionary<string, (double Bruto, double Desconto, HashSet<string> ChavesPedidoDistintos)>(
            StringComparer.OrdinalIgnoreCase);
        var pedidosDistintosResumo = new HashSet<string>(StringComparer.Ordinal);

        var grupos = resumoRows
            .Select((row, index) => (row, index))
            .GroupBy(x => ChaveGrupoResumoPorLojaNumeroPedido(x.row, x.index), StringComparer.Ordinal);

        foreach (var g in grupos)
        {
            var linhas = g.Select(x => x.row).ToList();
            var brutoPorLinha = linhas.Select(r => ParseBr(r.ValorBruto)).ToList();
            var totalBrutoPedido = brutoPorLinha.Sum();
            if (totalBrutoPedido < 1e-9)
            {
                continue;
            }

            pedidosDistintosResumo.Add(g.Key);

            var descontoPedido = 0.0;
            if (descontoPorPedido.TryGetValue(g.Key, out var dMap))
            {
                descontoPedido = Math.Max(0, dMap);
            }
            else
            {
                for (var i = 0; i < linhas.Count; i++)
                {
                    var vb = brutoPorLinha[i];
                    var vl = ParseBr(linhas[i].ValorLiquido);
                    descontoPedido += Math.Max(0, vb - vl);
                }
            }

            var rateios = RatearDescontoEntreLinhas(brutoPorLinha, descontoPedido);
            for (var i = 0; i < linhas.Count; i++)
            {
                var row = linhas[i];
                var plano = row.PlanoPagamento?.Trim();
                if (string.IsNullOrEmpty(plano))
                {
                    plano = row.MeioPagamento?.Trim();
                }

                if (string.IsNullOrEmpty(plano))
                {
                    continue;
                }

                var vb = brutoPorLinha[i];
                var dr = rateios[i];
                if (!porPlano.TryGetValue(plano, out var cur))
                {
                    cur = (0, 0, new HashSet<string>(StringComparer.Ordinal));
                }

                cur.ChavesPedidoDistintos.Add(g.Key);
                porPlano[plano] = (cur.Bruto + vb, cur.Desconto + dr, cur.ChavesPedidoDistintos);
            }
        }

        var list = porPlano
            .Select(kv =>
            {
                var bruto = kv.Value.Bruto;
                var desc = kv.Value.Desconto;
                var qtd = kv.Value.ChavesPedidoDistintos.Count;
                return new DescontoFormaPagamentoLinhaDto
                {
                    PlanoPagamento = kv.Key,
                    ValorBruto = bruto,
                    ValorDesconto = desc,
                    QuantidadeVendas = qtd,
                    DescontoPonderadoPercentual = bruto > 1e-9 ? desc / bruto * 100 : 0
                };
            })
            .OrderByDescending(x => x.ValorBruto)
            .ThenBy(x => x.PlanoPagamento, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), false))
            .ToList();

        if (list.Count > 0)
        {
            var tb = list.Sum(x => x.ValorBruto);
            var td = list.Sum(x => x.ValorDesconto);
            list.Add(new DescontoFormaPagamentoLinhaDto
            {
                PlanoPagamento = "TOTAL",
                ValorBruto = tb,
                ValorDesconto = td,
                QuantidadeVendas = pedidosDistintosResumo.Count,
                DescontoPonderadoPercentual = tb > 1e-9 ? td / tb * 100 : 0
            });
        }

        return list;
    }

    /// <summary>
    /// Código de loja para cruzamento: mesmo número com ou sem zeros à esquerda e com ou sem sufixo
    /// <c>" - NOME"</c> (ex.: resumo <c>1</c> / <c>0001</c> vs produtos <c>0001 - OTICA …</c>).
    /// </summary>
    private static string ChaveLojaNormalizada(string? lojaOuNome)
    {
        if (string.IsNullOrWhiteSpace(lojaOuNome))
        {
            return "—";
        }

        var t = lojaOuNome.Trim();
        var basePart = t;
        var sepIdx = t.IndexOf(" - ", StringComparison.Ordinal);
        if (sepIdx > 0)
        {
            basePart = t[..sepIdx].Trim();
        }

        var onlyDigits = new string(basePart.Where(char.IsDigit).ToArray());
        if (onlyDigits.Length > 0)
        {
            var trimmed = onlyDigits.TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        return t;
    }

    /// <summary>
    /// Chave de agrupamento no resumo: <c>Loja</c> + <c>NumeroPedido</c> (mesmo critério de <see cref="ChaveDescontoVendaLojaOs"/>).
    /// Sem <c>NumeroPedido</c> ou pedido não normalizável: grupo isolado <c>__linha_{índice}</c> por linha.
    /// </summary>
    private static string ChaveGrupoResumoPorLojaNumeroPedido(VendaFormaPagamentoResumoItemDto row, int indexNoResumo)
    {
        var loja = ChaveLojaNormalizada(row.Loja);
        if (string.IsNullOrWhiteSpace(row.NumeroPedido))
        {
            return $"{loja}{SepVenda}__linha_{indexNoResumo}";
        }

        var k = ChavePedidoIdentificador(row.NumeroPedido);
        if (string.IsNullOrEmpty(k))
        {
            return $"{loja}{SepVenda}__linha_{indexNoResumo}";
        }

        return $"{loja}{SepVenda}{k}";
    }

    /// <summary>
    /// Chave loja + O.S. (código da venda normalizado) para cruzar Produtos por O.S. com o resumo de formas de pagamento.
    /// </summary>
    private static string ChaveDescontoVendaLojaOs(ProdutoPorOsItem p)
    {
        var loja = ChaveLojaNormalizada(p.LojaNome);
        var cv = p.CodigoDaVenda?.Trim();
        if (string.IsNullOrEmpty(cv))
        {
            return $"{loja}{SepVenda}__sem_os__";
        }

        var k = ChavePedidoIdentificador(cv);
        return string.IsNullOrEmpty(k) ? $"{loja}{SepVenda}__sem_os__" : $"{loja}{SepVenda}{k}";
    }

    /// <summary>
    /// Desconto total da venda (O.S. + loja): diferença entre o bruto de catálogo (soma das linhas) e o líquido total da venda.
    /// Quando <c>ValorLiquidoTotalVenda</c> não vem (só zeros), usa-se <c>Σ(ValorBruto) − Σ(preço líquido linha)</c>,
    /// alinhado ao caso em que <c>Σ(PrecoTotal)</c> = líquido mas ainda há desconto de catálogo (ex. venda 76245).
    /// </summary>
    private static Dictionary<string, double> DescontoTotalPorVendaBrutoMenosLiquidoTotal(IReadOnlyList<ProdutoPorOsItem> produtosPorOs)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var g in produtosPorOs.GroupBy(ChaveDescontoVendaLojaOs, StringComparer.Ordinal))
        {
            var somaBruto = g.Sum(ValorBrutoLinhaSavWin);
            var liquidoVenda = 0.0;
            foreach (var p in g)
            {
                var liq = ParseBr(p.ValorLiquidoTotalVenda);
                if (liq > liquidoVenda)
                {
                    liquidoVenda = liq;
                }
            }

            double desconto;
            if (liquidoVenda > 1e-9)
            {
                desconto = Math.Max(0, somaBruto - liquidoVenda);
            }
            else
            {
                var somaPrecoLinha = g.Sum(ValorLinhaSavWin);
                desconto = Math.Max(0, somaBruto - somaPrecoLinha);
            }

            map[g.Key] = desconto;
        }

        return map;
    }

    /// <summary>Alinha <c>NumeroPedido</c> do resumo com <c>CodigoDaVenda</c> dos produtos (apenas dígitos, sem zeros à esquerda).</summary>
    private static string ChavePedidoIdentificador(string? numeroOuCodigo)
    {
        if (string.IsNullOrWhiteSpace(numeroOuCodigo))
        {
            return string.Empty;
        }

        var onlyDigits = new string(numeroOuCodigo.Where(char.IsDigit).ToArray());
        if (onlyDigits.Length == 0)
        {
            return numeroOuCodigo.Trim();
        }

        var trimmed = onlyDigits.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static double RoundMoney(double x) =>
        Math.Round(x, 2, MidpointRounding.AwayFromZero);

    /// <summary>Última linha absorve o restante para a soma bater com <paramref name="descontoTotalVenda"/>.</summary>
    private static List<double> RatearDescontoEntreLinhas(IReadOnlyList<double> valoresBrutoLinha, double descontoTotalVenda)
    {
        var n = valoresBrutoLinha.Count;
        if (n == 0)
        {
            return new List<double>();
        }

        var total = valoresBrutoLinha.Sum();
        if (total < 1e-9)
        {
            return Enumerable.Repeat(0.0, n).ToList();
        }

        var alvo = RoundMoney(Math.Max(0, descontoTotalVenda));
        var result = new List<double>(n);
        var restante = alvo;
        for (var i = 0; i < n; i++)
        {
            var vb = valoresBrutoLinha[i];
            if (i < n - 1)
            {
                var parte = RoundMoney(alvo * (vb / total));
                parte = Math.Min(parte, restante);
                result.Add(parte);
                restante = RoundMoney(restante - parte);
            }
            else
            {
                result.Add(RoundMoney(restante));
            }
        }

        return result;
    }

    private static List<FormaPagamentoLinhaDto> AgregarFormas(IReadOnlyList<VendaResumoFormaPagamentoItem> rows)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var meio = row.MeioPagamento?.Trim();
            if (string.IsNullOrEmpty(meio))
            {
                continue;
            }

            map[meio] = map.GetValueOrDefault(meio) + ParseBr(row.VendasValor);
        }

        return map
            .Select(kv => new FormaPagamentoLinhaDto { MeioPagamento = kv.Key, Valor = kv.Value })
            .OrderByDescending(x => x.Valor)
            .ThenBy(x => x.MeioPagamento, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), false))
            .ToList();
    }

    private static double PercentualDescontoVendaMedio(IReadOnlyList<ProdutoPorOsItem> rows)
    {
        var porVenda = new Dictionary<string, double>(StringComparer.Ordinal);
        var semCodigo = new List<double>();
        foreach (var p in rows)
        {
            var pct = ParseBr(p.DescontoPercentualDaVenda);
            var cv = p.CodigoDaVenda?.Trim() ?? "";
            if (!string.IsNullOrEmpty(cv))
            {
                porVenda[ChaveAgrupamentoVenda(p)] = pct;
            }
            else
            {
                semCodigo.Add(pct);
            }
        }

        var vals = porVenda.Values.Concat(semCodigo).ToList();
        if (vals.Count == 0)
        {
            return 0;
        }

        var media = vals.Average();
        return Math.Min(100, Math.Max(0, media));
    }

    private static double ValorLinhaSavWin(ProdutoPorOsItem p)
    {
        var pt = ParseBr(p.PrecoTotalProduto);
        if (pt > 0)
        {
            return pt;
        }

        var vb = ParseBr(p.ValorBruto);
        return vb > 0 ? vb : 0;
    }

    private static double ValorBrutoLinhaSavWin(ProdutoPorOsItem p)
    {
        var vb = ParseBr(p.ValorBruto);
        return vb > 0 ? vb : ValorLinhaSavWin(p);
    }

    private static double ValorLiquidoLinhaAlocado(
        ProdutoPorOsItem p,
        Dictionary<string, double> somaPrecoPorVenda,
        Dictionary<string, double> liquidoPorVenda,
        bool faturamentoUsaLiquidoPorVenda)
    {
        var precoLinha = ValorLinhaSavWin(p);
        var kAgg = ChaveAgrupamentoVenda(p);
        var sumV = somaPrecoPorVenda.GetValueOrDefault(kAgg);
        liquidoPorVenda.TryGetValue(kAgg, out var liqV);
        if (liqV > 0 && sumV > 0)
        {
            return precoLinha / sumV * liqV;
        }

        return faturamentoUsaLiquidoPorVenda ? 0 : precoLinha;
    }

    private static string ChaveAgrupamentoVenda(ProdutoPorOsItem p)
    {
        var loja = string.IsNullOrWhiteSpace(p.LojaNome) ? "—" : p.LojaNome.Trim();
        var cv = p.CodigoDaVenda?.Trim() ?? "";
        return string.IsNullOrEmpty(cv) ? $"{loja}{SepVenda}__sem_os__" : $"{loja}{SepVenda}{cv}";
    }

    /// <summary>
    /// Chave única da venda só por <c>CODIGODAVENDA</c> (conforme cadastro SavWin), para contagem de vendas por categoria.
    /// </summary>
    private static string? CodigoDaVendaAgrupamento(ProdutoPorOsItem p)
    {
        var cv = p.CodigoDaVenda?.Trim();
        return string.IsNullOrEmpty(cv) ? null : cv;
    }

    private static int ContarVendasOsDistintas(IReadOnlyList<ProdutoPorOsItem> rows)
    {
        var chaves = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in rows)
        {
            if (string.IsNullOrWhiteSpace(p.CodigoDaVenda))
            {
                continue;
            }

            chaves.Add(ChaveAgrupamentoVenda(p));
        }

        return chaves.Count;
    }

    private enum SubgrupoProduto
    {
        Outros,
        Armacoes,
        Lentes,
        Servicos
    }

    private static SubgrupoProduto SubgrupoPorPrefixoCodigoProduto(string? codigo)
    {
        var c = codigo?.Trim() ?? "";
        if (c.Length == 0)
        {
            return SubgrupoProduto.Outros;
        }

        if (c.StartsWith("01", StringComparison.Ordinal))
        {
            return SubgrupoProduto.Armacoes;
        }

        if (c.StartsWith("02", StringComparison.Ordinal))
        {
            return SubgrupoProduto.Lentes;
        }

        if (c.StartsWith("03", StringComparison.Ordinal))
        {
            return SubgrupoProduto.Servicos;
        }

        if (c.StartsWith('1'))
        {
            return SubgrupoProduto.Armacoes;
        }

        if (c.StartsWith('2'))
        {
            return SubgrupoProduto.Lentes;
        }

        if (c.StartsWith('3'))
        {
            return SubgrupoProduto.Servicos;
        }

        return SubgrupoProduto.Outros;
    }

    private static double SomaLiquidoEmMapas(
        IReadOnlyList<Dictionary<string, (double Bruto, double Liquido, double Qtd)>> maps)
    {
        double s = 0;
        foreach (var m in maps)
        {
            foreach (var agg in m.Values)
            {
                s += agg.Liquido;
            }
        }

        return s;
    }

    private static List<VendaGrifeSubgrupoDto> MontarVendasPorGrifeSubgrupos(
        Dictionary<string, (double Bruto, double Liquido, double Qtd)> armacoes,
        Dictionary<string, (double Bruto, double Liquido, double Qtd)> lentes,
        Dictionary<string, (double Bruto, double Liquido, double Qtd)> servicos)
    {
        var maps = new List<Dictionary<string, (double Bruto, double Liquido, double Qtd)>> { armacoes, lentes, servicos };
        var totalLiqCard = SomaLiquidoEmMapas(maps);
        var defs = new[]
        {
            ("Armações", armacoes),
            ("Lentes", lentes),
            ("Serviços", servicos)
        };

        var result = new List<VendaGrifeSubgrupoDto>();
        foreach (var (titulo, m) in defs)
        {
            var linhasRaw = m
                .Select(kv => (Grife: kv.Key, kv.Value.Bruto, kv.Value.Liquido, kv.Value.Qtd))
                .OrderByDescending(x => x.Liquido)
                .ThenBy(x => x.Grife, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), false))
                .ToList();

            var linhas = linhasRaw.Select(l => new VendaGrifeLinhaDto
            {
                Grife = l.Grife,
                Bruto = l.Bruto,
                Liquido = l.Liquido,
                Quantidade = l.Qtd,
                Percentual = totalLiqCard > 0 ? l.Liquido / totalLiqCard * 100 : 0
            }).ToList();

            result.Add(new VendaGrifeSubgrupoDto { Titulo = titulo, Linhas = linhas });
        }

        return result;
    }

    public static double ParseBr(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return 0;
        }

        var t = s.Trim();
        if (t.Contains(',') && t.Contains('.'))
        {
            t = t.Replace(".", "", StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal);
        }
        else if (t.Contains(','))
        {
            t = t.Replace(",", ".", StringComparison.Ordinal);
        }

        return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static List<double> BarsFromSeries(IReadOnlyList<double> values, int buckets = BucketsBarras)
    {
        if (values.Count == 0)
        {
            return Enumerable.Repeat(0.0, buckets).ToList();
        }

        var chunk = Math.Max(1, (int)Math.Ceiling(values.Count / (double)buckets));
        var sums = new List<double>(buckets);
        for (var i = 0; i < buckets; i++)
        {
            var start = i * chunk;
            var slice = values.Skip(start).Take(chunk).ToList();
            sums.Add(slice.Sum());
        }

        var max = sums.Max();
        if (max < 1e-9)
        {
            max = 1e-9;
        }

        return sums.Select(x => Math.Round(x / max * 100)).ToList();
    }
}
