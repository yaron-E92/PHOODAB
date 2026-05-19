import { describe, it, expect } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { ReplenishmentSuggestionsList } from './main';

describe('replenishment rendering contract', () => {
  it('renders backend-provided requiredAmount and unit without frontend recalculation', () => {
    const apiPayload = [
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        requiredAmount: 4,
        desiredQuantity: 10,
        currentQuantity: 9,
        unit: 'liter'
      }
    ];

    const html = renderToStaticMarkup(<ReplenishmentSuggestionsList suggestions={apiPayload} />);

    expect(html).toContain('Milk: 4 liter');
    expect(html).not.toContain('Milk: 1 liter');
  });
});
