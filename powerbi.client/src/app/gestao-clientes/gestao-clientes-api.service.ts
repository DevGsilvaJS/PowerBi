import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface GestaoClienteDto {
  id: number;
  usuario: string;
  chaveWs: string;
  identificador: string;
  lojas: string;
  criadoEm: string;
}

export interface GestaoClienteCreatePayload {
  usuario: string;
  senha: string;
  chaveWs: string;
  identificador: string;
  lojas: string;
}

export interface GestaoClienteUpdatePayload {
  usuario: string;
  chaveWs: string;
  identificador: string;
  lojas: string;
  /** Se definida e não vazia, altera a senha no servidor. */
  senha?: string;
}

@Injectable({
  providedIn: 'root'
})
export class GestaoClientesApiService {
  private readonly baseUrl = `${environment.apiBaseUrl.replace(/\/$/, '')}/api/GestaoClientes`;

  constructor(private readonly http: HttpClient) {}

  listar(): Observable<GestaoClienteDto[]> {
    return this.http.get<GestaoClienteDto[]>(this.baseUrl);
  }

  obter(id: number): Observable<GestaoClienteDto> {
    return this.http.get<GestaoClienteDto>(`${this.baseUrl}/${id}`);
  }

  criar(payload: GestaoClienteCreatePayload): Observable<GestaoClienteDto> {
    return this.http.post<GestaoClienteDto>(this.baseUrl, payload);
  }

  atualizar(id: number, payload: GestaoClienteUpdatePayload): Observable<GestaoClienteDto> {
    return this.http.put<GestaoClienteDto>(`${this.baseUrl}/${id}`, payload);
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
