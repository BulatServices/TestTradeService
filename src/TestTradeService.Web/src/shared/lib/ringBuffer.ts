export class RingBuffer<T> {
  private readonly maxSize: number;
  private values: T[];

  constructor(maxSize: number) {
    this.maxSize = maxSize;
    this.values = [];
  }

  push(item: T): void {
    this.values.push(item);
    if (this.values.length > this.maxSize) {
      this.values.shift();
    }
  }

  clear(): void {
    this.values = [];
  }

  toArray(): T[] {
    return [...this.values];
  }

  get size(): number {
    return this.values.length;
  }
}

