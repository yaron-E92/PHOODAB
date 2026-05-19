import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import { getHealth, getReplenishmentSuggestions, getVersion, type ReplenishmentSuggestion } from '../../../packages/api-client/src/client';

function App() {
  const baseUrl = 'http://localhost:5199';
  const [health, setHealth] = useState<string>('loading');
  const [version, setVersion] = useState<string>('loading');
  const [suggestions, setSuggestions] = useState<ReplenishmentSuggestion[]>([]);

  useEffect(() => {
    getHealth(baseUrl).then((r) => setHealth(r.status)).catch((e) => setHealth(String(e)));
    getVersion(baseUrl).then((r) => setVersion(r.version)).catch((e) => setVersion(String(e)));
    getReplenishmentSuggestions(baseUrl).then(setSuggestions).catch(() => setSuggestions([]));
  }, []);

  return (
    <main>
      <h1>PHOODAB Web Shell</h1>
      <p>Health: {health}</p>
      <p>Version: {version}</p>
      <h2>Replenishment Suggestions</h2>
      <ul>
        {suggestions.map((suggestion) => (
          <li key={suggestion.itemDefinitionId}>
            {suggestion.itemName}: {suggestion.requiredAmount} {suggestion.unit}
          </li>
        ))}
      </ul>
    </main>
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(<App />);
