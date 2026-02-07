export class ApiContractError extends Error {
  constructor(message = 'Несовместимый формат данных от сервера') {
    super(message);
    this.name = 'ApiContractError';
  }
}

export class ApiError extends Error {
  public readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

