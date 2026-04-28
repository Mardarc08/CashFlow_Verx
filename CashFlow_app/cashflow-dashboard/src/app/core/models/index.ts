export enum TipoLancamento {
  Debito = 1,
  Credito = 2
}

export enum MeioLancamento {
  Dinheiro = 1,
  CartaoCredito = 2,
  CartaoDebito = 3,
  Transferencia = 4,
  Pix = 5
}

export interface Lancamento {
  id: string;
  valor: number;
  tipo: TipoLancamento;
  tipoDescricao: string;
  descricao: string;
  data: string;
  criadoEm: string;
}

export interface RegistrarLancamentoRequest {
  valor: number;
  tipo: TipoLancamento;
  descricao: string;
  data: string;
  meioLancamento?: MeioLancamento;
}

export interface RegistrarLancamentoResponse {
  id: string;
  criadoEm: string;
}

export interface ConsolidadoDiario {
  data: string;
  totalCreditos: number;
  totalDebitos: number;
  saldoFinal: number;
  atualizadoEm: string;
}
