import { Component, OnInit } from '@angular/core';
import { GestaoClientesConfigService } from './gestao-clientes-config.service';
import {
  GestaoClienteCreatePayload,
  GestaoClienteDto,
  GestaoClienteUpdatePayload,
  GestaoClientesApiService
} from './gestao-clientes-api.service';

/** Senha de acesso à URL oculta (definida pelo cliente). */
const SENHA_ACESSO = 'Defdesp2012@';

type ModoForm = 'none' | 'create' | 'edit';

@Component({
  selector: 'app-gestao-clientes',
  templateUrl: './gestao-clientes.component.html',
  styleUrl: './gestao-clientes.component.css'
})
export class GestaoClientesComponent implements OnInit {
  desbloqueado = false;
  senhaDigitada = '';
  senhaErro = '';

  lista: GestaoClienteDto[] = [];
  carregandoLista = false;
  apiErro = '';

  modoForm: ModoForm = 'none';
  editandoId: number | null = null;

  form = {
    usuario: '',
    senha: '',
    chaveWs: '',
    identificador: '',
    lojas: ''
  };

  salvando = false;
  salvoMsg = '';
  salvoErro = '';

  constructor(
    private readonly cfg: GestaoClientesConfigService,
    private readonly api: GestaoClientesApiService
  ) {}

  ngOnInit(): void {
    this.desbloqueado = this.cfg.isSessionUnlocked();
    if (this.desbloqueado) {
      this.carregarLista();
    }
  }

  tentarSenha(): void {
    this.senhaErro = '';
    if (this.senhaDigitada === SENHA_ACESSO) {
      this.cfg.setSessionUnlocked(true);
      this.desbloqueado = true;
      this.senhaDigitada = '';
      this.carregarLista();
    } else {
      this.senhaErro = 'Senha incorreta.';
    }
  }

  carregarLista(): void {
    this.apiErro = '';
    this.carregandoLista = true;
    this.api.listar().subscribe({
      next: (dados) => {
        this.lista = dados;
        this.carregandoLista = false;
      },
      error: (err) => {
        this.carregandoLista = false;
        this.apiErro = this.mensagemHttp(err);
      }
    });
  }

  novo(): void {
    this.modoForm = 'create';
    this.editandoId = null;
    this.limparForm();
    this.salvoMsg = '';
    this.salvoErro = '';
  }

  editar(item: GestaoClienteDto): void {
    this.modoForm = 'edit';
    this.editandoId = item.id;
    this.form = {
      usuario: item.usuario,
      senha: '',
      chaveWs: item.chaveWs,
      identificador: item.identificador,
      lojas: item.lojas ?? ''
    };
    this.salvoMsg = '';
    this.salvoErro = '';
  }

  cancelarForm(): void {
    this.modoForm = 'none';
    this.editandoId = null;
    this.limparForm();
    this.salvoErro = '';
  }

  salvar(): void {
    this.salvoMsg = '';
    this.salvoErro = '';

    if (this.modoForm === 'create') {
      if (!this.form.usuario?.trim() || !this.form.senha) {
        this.salvoErro = 'Preencha usuário e senha.';
        return;
      }
      const payload: GestaoClienteCreatePayload = {
        usuario: this.form.usuario.trim(),
        senha: this.form.senha,
        chaveWs: this.form.chaveWs.trim(),
        identificador: this.form.identificador.trim(),
        lojas: this.form.lojas?.trim() ?? ''
      };
      this.salvando = true;
      this.api.criar(payload).subscribe({
        next: () => {
          this.salvando = false;
          this.salvoMsg = 'Cadastro criado.';
          this.cancelarForm();
          this.carregarLista();
        },
        error: (err) => {
          this.salvando = false;
          this.salvoErro = this.mensagemHttp(err);
        }
      });
      return;
    }

    if (this.modoForm === 'edit' && this.editandoId != null) {
      if (!this.form.usuario?.trim()) {
        this.salvoErro = 'Preencha o usuário.';
        return;
      }
      const payload: GestaoClienteUpdatePayload = {
        usuario: this.form.usuario.trim(),
        chaveWs: this.form.chaveWs.trim(),
        identificador: this.form.identificador.trim(),
        lojas: this.form.lojas?.trim() ?? ''
      };
      const novaSenha = this.form.senha?.trim();
      if (novaSenha) {
        payload.senha = novaSenha;
      }
      this.salvando = true;
      this.api.atualizar(this.editandoId, payload).subscribe({
        next: () => {
          this.salvando = false;
          this.salvoMsg = 'Cadastro atualizado.';
          this.cancelarForm();
          this.carregarLista();
        },
        error: (err) => {
          this.salvando = false;
          this.salvoErro = this.mensagemHttp(err);
        }
      });
    }
  }

  excluir(item: GestaoClienteDto): void {
    if (!confirm(`Excluir o cadastro "${item.usuario}" (id ${item.id})?`)) {
      return;
    }
    this.apiErro = '';
    this.api.excluir(item.id).subscribe({
      next: () => {
        this.salvoMsg = 'Registro excluído.';
        if (this.modoForm === 'edit' && this.editandoId === item.id) {
          this.cancelarForm();
        }
        this.carregarLista();
      },
      error: (err) => {
        this.apiErro = this.mensagemHttp(err);
      }
    });
  }

  encerrarSessao(): void {
    this.cfg.setSessionUnlocked(false);
    this.desbloqueado = false;
    this.senhaDigitada = '';
    this.senhaErro = '';
    this.salvoMsg = '';
    this.lista = [];
    this.cancelarForm();
  }

  private limparForm(): void {
    this.form = {
      usuario: '',
      senha: '',
      chaveWs: '',
      identificador: '',
      lojas: ''
    };
  }

  private mensagemHttp(err: unknown): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const e = err as { error?: unknown };
      if (typeof e.error === 'string' && e.error.length) {
        return e.error;
      }
      if (e.error && typeof e.error === 'object' && 'title' in e.error) {
        return String((e.error as { title?: string }).title);
      }
    }
    return 'Falha na comunicação com o servidor. Verifique se a API está em execução.';
  }
}
