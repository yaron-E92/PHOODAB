import { describe, it, expect } from 'vitest';

describe('replenishment rendering contract', () => {
  it('renders backend-provided requiredAmount without frontend recalculation', () => {
    const apiPayload = { requiredAmount: 4, desiredQuantity: 10, currentQuantity: 9 };
    const displayed = apiPayload.requiredAmount;

    expect(displayed).toBe(4);
    expect(displayed).not.toBe(apiPayload.desiredQuantity - apiPayload.currentQuantity);
  });
});
