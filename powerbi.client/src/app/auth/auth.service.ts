import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

const KEY_TOKEN = 'ga_auth_token';
const KEY_USUARIO = 'ga_auth_usuario';
const KEY_LOJAS = 'ga_auth_lojas';

export interface LoginResponse {
  accessToken: string;
  usuario: string;
  lojas: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiAuth = `${environment.apiBaseUrl.replace(/\/$/, '')}/api/Auth/login`;

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router
  ) {}

  login(usuario: string, senha: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(this.apiAuth, { usuario, senha }).pipe(
      tap((res) => {
        sessionStorage.setItem(KEY_TOKEN, res.accessToken);
        sessionStorage.setItem(KEY_USUARIO, res.usuario);
        sessionStorage.setItem(KEY_LOJAS, res.lojas ?? '');
      })
    );
  }

  logout(): void {
    sessionStorage.removeItem(KEY_TOKEN);
    sessionStorage.removeItem(KEY_USUARIO);
    sessionStorage.removeItem(KEY_LOJAS);
    void this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return sessionStorage.getItem(KEY_TOKEN);
  }

  getUsuario(): string {
    return sessionStorage.getItem(KEY_USUARIO) ?? '';
  }

  /** String de lojas do cadastro (ex.: 1,2,3). */
  getLojasCadastro(): string {
    return sessionStorage.getItem(KEY_LOJAS) ?? '';
  }

  isAuthenticated(): boolean {
    return !!this.getToken()?.trim();
  }
}
