import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  usuario = '';
  senha = '';
  erro = '';
  carregando = false;

  constructor(
    private readonly router: Router,
    private readonly auth: AuthService
  ) {}

  logar(): void {
    this.erro = '';
    if (!this.usuario?.trim() || !this.senha) {
      this.erro = 'Informe usuário e senha.';
      return;
    }
    this.carregando = true;
    this.auth.login(this.usuario.trim(), this.senha).subscribe({
      next: () => {
        this.carregando = false;
        void this.router.navigate(['/app', 'resumo-geral']);
      },
      error: () => {
        this.carregando = false;
        this.erro = 'Usuário ou senha inválidos, ou cadastro inexistente em Gestão de clientes.';
      }
    });
  }
}
