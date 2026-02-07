import { describe, expect, it } from 'vitest';
import { RingBuffer } from './ringBuffer';

describe('RingBuffer', () => {
  it('хранит только последние N элементов', () => {
    const buffer = new RingBuffer<number>(3);
    buffer.push(1);
    buffer.push(2);
    buffer.push(3);
    buffer.push(4);

    expect(buffer.toArray()).toEqual([2, 3, 4]);
  });

  it('очищает буфер', () => {
    const buffer = new RingBuffer<string>(2);
    buffer.push('a');
    buffer.clear();

    expect(buffer.size).toBe(0);
  });
});

