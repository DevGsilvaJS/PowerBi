import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import * as L from 'leaflet';
import { AuthService } from '../auth/auth.service';
import { VendaPorBairroItem } from '../relatorios/venda-por-bairro.model';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';
import {
  LojaOption,
  combinarLojasCadastroComSavwin,
  lojaIdsParaParametroApi
} from '../shared/lojas-filtro';

@Component({
  selector: 'app-estatisticas-cliente',
  templateUrl: './estatisticas-cliente.component.html',
  styleUrl: './estatisticas-cliente.component.css'
})
export class EstatisticasClienteComponent implements OnInit, OnDestroy {
  dataInicial = '';
  dataFinal = '';
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  /** Texto para filtrar bairros / cidade na lista e no mapa (após carregar). */
  filtroBairro = '';

  periodoErro = '';
  apiErro = '';
  carregando = false;
  jaPesquisou = false;

  linhasDados: VendaPorBairroItem[] = [];

  /**
   * Coordenadas para o mapa quando a API não envia lat/lon — chave = bairro + cidade (normalizado).
   * Posições em espiral ao redor de um centro (visualização aproximada, não escala cartográfica exata).
   */
  private coordsFallbackPorChave = new Map<string, { lat: number; lon: number }>();

  private map?: L.Map;
  private markersLayer?: L.LayerGroup;
  private mapResizeObserver?: ResizeObserver;

  constructor(
    private readonly auth: AuthService,
    private readonly relatorios: RelatoriosApiService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const hoje = new Date();
    this.dataInicial = this.toInputDate(hoje);
    this.dataFinal = this.toInputDate(hoje);
    this.relatorios.getLojasSavwin().subscribe((items) => {
      this.lojas = combinarLojasCadastroComSavwin(this.auth.getLojasCadastro(), items);
      this.lojaIdsSelecionadas = [];
      this.cdr.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.mapResizeObserver?.disconnect();
    this.mapResizeObserver = undefined;
    this.map?.remove();
    this.map = undefined;
    this.markersLayer = undefined;
  }

  get linhasFiltradas(): VendaPorBairroItem[] {
    const q = this.filtroBairro.trim().toLowerCase();
    if (!q) {
      return this.linhasDados;
    }
    return this.linhasDados.filter((x) => {
      const b = (x.bairro ?? '').toLowerCase();
      const c = (x.cidade ?? '').toLowerCase();
      return b.includes(q) || c.includes(q);
    });
  }

  /** Há dados carregados — o mapa permanece visível; os marcadores seguem o filtro “Pesquisar”. */
  get temDadosParaMapa(): boolean {
    return this.linhasDados.length > 0;
  }

  /** Pelo menos um ponto não tem coordenadas da API (usa posição aproximada no mapa). */
  get mapaUsaPosicaoAproximada(): boolean {
    return this.linhasDados.some(
      (r) =>
        r.lat == null ||
        r.lon == null ||
        !Number.isFinite(r.lat) ||
        !Number.isFinite(r.lon)
    );
  }

  /** Lat/lng efetivos para o mapa (API ou fallback em espiral). */
  private coordenadaParaMapa(r: VendaPorBairroItem): { lat: number; lon: number } {
    if (
      r.lat != null &&
      r.lon != null &&
      Number.isFinite(r.lat) &&
      Number.isFinite(r.lon)
    ) {
      return { lat: r.lat, lon: r.lon };
    }
    const k = this.chaveMapa(r);
    const hit = this.coordsFallbackPorChave.get(k);
    return hit ?? { lat: -22.58, lon: -47.4 };
  }

  private chaveMapa(r: VendaPorBairroItem): string {
    return `${(r.bairro || '').trim().toLowerCase()}\u001f${(r.cidade || '').trim().toLowerCase()}`;
  }

  private preencherFallbacksParaLinhasUnicas(): void {
    this.coordsFallbackPorChave.clear();
    const visto = new Set<string>();
    const ordem: VendaPorBairroItem[] = [];
    for (const r of this.linhasDados) {
      const k = this.chaveMapa(r);
      if (visto.has(k)) {
        continue;
      }
      visto.add(k);
      ordem.push(r);
    }
    ordem.forEach((r, idx) => {
      if (
        r.lat != null &&
        r.lon != null &&
        Number.isFinite(r.lat) &&
        Number.isFinite(r.lon)
      ) {
        return;
      }
      const k = this.chaveMapa(r);
      const angle = (idx / Math.max(ordem.length, 1)) * Math.PI * 2 * 2.35;
      const rings = 0.045 + (idx % 8) * 0.008;
      const baseLat = -22.58;
      const baseLon = -47.4;
      this.coordsFallbackPorChave.set(k, {
        lat: baseLat + rings * Math.sin(angle),
        lon: baseLon + rings * Math.cos(angle)
      });
    });
  }

  get linhasRankingDesc(): VendaPorBairroItem[] {
    return [...this.linhasFiltradas].sort((a, b) => b.valorLiquido - a.valorLiquido);
  }

  pesquisar(): void {
    this.periodoErro = '';
    this.apiErro = '';
    if (!this.dataInicial?.trim() || !this.dataFinal?.trim()) {
      this.periodoErro = 'Informe a data inicial e a data final.';
      return;
    }
    const tIni = Date.parse(this.dataInicial + 'T12:00:00');
    const tFim = Date.parse(this.dataFinal + 'T12:00:00');
    if (Number.isNaN(tIni) || Number.isNaN(tFim)) {
      this.periodoErro = 'Datas inválidas.';
      return;
    }
    if (tFim < tIni) {
      this.periodoErro = 'A data final não pode ser anterior à data inicial.';
      return;
    }
    if (this.lojas.length > 0 && this.lojaIdsSelecionadas.length === 0) {
      this.periodoErro = 'Selecione ao menos uma loja.';
      return;
    }

    const lojaParam = lojaIdsParaParametroApi(this.lojas, this.lojaIdsSelecionadas);
    this.carregando = true;
    this.jaPesquisou = true;
    this.cdr.markForCheck();

    this.relatorios
      .vendasPorBairro({
        dataInicial: this.dataInicial,
        dataFinal: this.dataFinal,
        lojaId: lojaParam ?? undefined
      })
      .subscribe({
        next: (rows) => {
          this.linhasDados = rows ?? [];
          this.apiErro = '';
          this.carregando = false;
          this.preencherFallbacksParaLinhasUnicas();
          this.cdr.markForCheck();
          setTimeout(() => {
            if (this.linhasDados.length > 0) {
              this.ensureMap();
              this.atualizarMarcadores();
            } else {
              this.mapResizeObserver?.disconnect();
              this.mapResizeObserver = undefined;
              this.map?.remove();
              this.map = undefined;
              this.markersLayer = undefined;
            }
          }, 0);
        },
        error: (err: unknown) => {
          this.linhasDados = [];
          this.carregando = false;
          this.apiErro = this.mensagemErroApi(err);
          this.mapResizeObserver?.disconnect();
          this.mapResizeObserver = undefined;
          this.map?.remove();
          this.map = undefined;
          this.markersLayer = undefined;
          this.cdr.markForCheck();
        }
      });
  }

  onFiltroBairroChange(): void {
    setTimeout(() => {
      if (!this.linhasDados.length) {
        return;
      }
      this.ensureMap();
      this.atualizarMarcadores();
    }, 0);
  }

  formatMoeda(v: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v);
  }

  private mensagemErroApi(err: unknown): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const e = err as { error?: unknown };
      if (typeof e.error === 'string' && e.error.trim().length > 0) {
        return e.error.trim();
      }
    }
    return 'Não foi possível carregar vendas por bairro. Tente novamente ou confira a integração SavWin.';
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  /**
   * Leaflet mede o div na criação; se o painel ainda não tem largura final, o mapa fica “cortado” (faixa preta).
   * Recalcula tamanho várias vezes após layout + observa redimensionamento do contêiner.
   */
  private scheduleMapResize(): void {
    const inv = () => {
      if (!this.map) {
        return;
      }
      this.map.invalidateSize({ animate: false });
    };
    requestAnimationFrame(inv);
    setTimeout(inv, 0);
    setTimeout(inv, 80);
    setTimeout(inv, 250);
    setTimeout(inv, 600);
  }

  private ensureMap(): void {
    const el = document.getElementById('est-cli-map');
    if (!el) {
      return;
    }

    if (this.map) {
      try {
        const c = this.map.getContainer();
        if (!document.body.contains(c)) {
          this.mapResizeObserver?.disconnect();
          this.mapResizeObserver = undefined;
          this.map.remove();
          this.map = undefined;
          this.markersLayer = undefined;
        } else {
          this.scheduleMapResize();
          return;
        }
      } catch {
        this.map = undefined;
        this.markersLayer = undefined;
      }
    }

    this.map = L.map(el, {
      zoomControl: true,
      attributionControl: true
    }).setView([-22.58, -47.4], 10);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap',
      maxZoom: 19,
      minZoom: 3
    }).addTo(this.map);

    this.markersLayer = L.layerGroup().addTo(this.map);

    this.map.whenReady(() => this.scheduleMapResize());

    if (typeof ResizeObserver !== 'undefined') {
      this.mapResizeObserver?.disconnect();
      this.mapResizeObserver = new ResizeObserver(() => this.scheduleMapResize());
      this.mapResizeObserver.observe(el);
    }
  }

  private atualizarMarcadores(): void {
    if (!this.map || !this.markersLayer) {
      return;
    }
    this.markersLayer.clearLayers();
    const rows = this.linhasRankingDesc;
    if (rows.length === 0) {
      this.map.setView([-22.58, -47.4], 9);
      this.scheduleMapResize();
      return;
    }
    const maxV = Math.max(...rows.map((r) => r.valorLiquido), 1);
    const pontos: L.LatLngTuple[] = [];
    for (const r of rows) {
      const { lat, lon } = this.coordenadaParaMapa(r);
      if (!Number.isFinite(lat) || !Number.isFinite(lon)) {
        continue;
      }
      pontos.push([lat, lon]);
      const t = r.valorLiquido / maxV;
      const radius = Math.max(12, 8 + t * 26);
      const cm = L.circleMarker([lat, lon], {
        radius,
        stroke: true,
        color: '#7dd3fc',
        weight: 3,
        fillColor: '#0ea5e9',
        fillOpacity: 0.72
      });
      const sub = r.cidade ? `<br/><span>${this._esc(r.cidade)}</span>` : '';
      cm.bindPopup(
        `<strong>${this._esc(r.bairro)}</strong>${sub}<br/>${this.formatMoeda(r.valorLiquido)}<br/>` +
          `${r.qtdVendasDistintas} vendas (O.S. dist.)`
      );
      cm.addTo(this.markersLayer);
    }
    if (pontos.length === 0) {
      this.map.setView([-22.58, -47.4], 9);
      this.scheduleMapResize();
      return;
    }

    if (pontos.length === 1) {
      this.map.setView(pontos[0], 13);
    } else {
      const bounds = L.latLngBounds(pontos);
      const ne = bounds.getNorthEast();
      const sw = bounds.getSouthWest();
      const mesmoPonto = ne.lat === sw.lat && ne.lng === sw.lng;
      if (mesmoPonto) {
        this.map.setView(pontos[0], 14);
      } else {
        this.map.fitBounds(bounds.pad(0.22));
      }
    }
    this.scheduleMapResize();
  }

  private _esc(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
}
