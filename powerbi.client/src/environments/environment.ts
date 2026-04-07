/** Desenvolvimento (`ng serve`): chama o Kestrel diretamente (evita falha do proxy no POST). */
export const environment = {
  production: false,
  /** Base da API (sem barra final). Em produção, use string vazia para mesmo host. */
  apiBaseUrl: 'https://localhost:7201'
};
