import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import { getHealth, getVersion } from '../../../packages/api-client/src/client';

function App() {
  const baseUrl = 'http://localhost:5199';
  const [health, setHealth] = useState<string>('loading');
  const [version, setVersion] = useState<string>('loading');

  useEffect(() => {
    getHealth(baseUrl).then((r) => setHealth(r.status)).catch((e) => setHealth(String(e)));
    getVersion(baseUrl).then((r) => setVersion(r.version)).catch((e) => setVersion(String(e)));
  }, []);

  return (
    <main>
      <h1>PHOODAB Web Shell</h1>
      <p>Health: {health}</p>
      <p>Version: {version}</p>
    </main>
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(<App />);
