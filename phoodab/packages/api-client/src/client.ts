import type { paths } from './generated';

type HealthResponse = paths['/health']['get']['responses']['200']['content']['application/json'];
type VersionResponse = paths['/version']['get']['responses']['200']['content']['application/json'];

export async function getHealth(baseUrl: string): Promise<HealthResponse> {
  const response = await fetch(`${baseUrl}/health`);
  if (!response.ok) throw new Error(`Health failed: ${response.status}`);
  return response.json();
}

export async function getVersion(baseUrl: string): Promise<VersionResponse> {
  const response = await fetch(`${baseUrl}/version`);
  if (!response.ok) throw new Error(`Version failed: ${response.status}`);
  return response.json();
}
