import { Injectable } from '@angular/core';

export interface GestaoClientesConfig {
  usuario: string;
  senha: string;
  chaveWs: string;
  identificador: string;
  /** Lista de IDs de lojas separados por vírgula, ex.: `1,2,3,4,5` */
  lojas: string;
}

const STORAGE_KEY = 'ga_gestaoclientes_config';
const SESSION_AUTH_KEY = 'ga_gestaoclientes_sessao';

@Injectable({
  providedIn: 'root'
})
export class GestaoClientesConfigService {
  getConfig(): GestaoClientesConfig | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return null;
      }
      const parsed = JSON.parse(raw) as GestaoClientesConfig;
      return {
        usuario: parsed.usuario ?? '',
        senha: parsed.senha ?? '',
        chaveWs: parsed.chaveWs ?? '',
        identificador: parsed.identificador ?? '',
        lojas: parsed.lojas ?? ''
      };
    } catch {
      return null;
    }
  }

  saveConfig(config: GestaoClientesConfig): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(config));
  }

  isSessionUnlocked(): boolean {
    return sessionStorage.getItem(SESSION_AUTH_KEY) === '1';
  }

  setSessionUnlocked(value: boolean): void {
    if (value) {
      sessionStorage.setItem(SESSION_AUTH_KEY, '1');
    } else {
      sessionStorage.removeItem(SESSION_AUTH_KEY);
    }
  }

  /** Converte a string de lojas (`1,2,3`) em lista de IDs para parâmetros de API. */
  parseLojasIds(lojas: string): string[] {
    return lojas
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s.length > 0);
  }
}
