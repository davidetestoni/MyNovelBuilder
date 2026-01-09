export interface IntegrationsConfigDto {
  hasOpenRouterApiKey: boolean;
}

export interface UpdateIntegrationsConfigDto {
  openRouterApiKey?: string | null;
}
