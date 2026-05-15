export type HealthResponse = { status: string };
export type VersionResponse = { version: string; informationalVersion: string };

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
