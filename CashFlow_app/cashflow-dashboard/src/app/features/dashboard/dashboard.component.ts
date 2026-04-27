import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { LancamentosService } from '../../core/services/lancamentos.service';
import { ConsolidadoService } from '../../core/services/consolidado.service';
import { Lancamento, ConsolidadoDiario, TipoLancamento, MeioLancamento } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DatePipe, CurrencyPipe],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  // ── Estado ────────────────────────────────────────────────────────────────
  hoje = signal(new Date().toISOString().split('T')[0]);
  consolidadoHoje = signal<ConsolidadoDiario | null>(null);
  lancamentos = signal<Lancamento[]>([]);
  historico = signal<ConsolidadoDiario[]>([]);

  carregandoConsolidado = signal(false);
  carregandoLancamentos = signal(false);
  carregandoHistorico = signal(false);
  salvandoLancamento = signal(false);

  mensagemSucesso = signal('');
  mensagemErro = signal('');

  TipoLancamento = TipoLancamento;
  MeioLancamento = MeioLancamento;

  // ── Computed ──────────────────────────────────────────────────────────────
  saldoPositivo = computed(() => (this.consolidadoHoje()?.saldoFinal ?? 0) >= 0);

  form!: FormGroup;

  constructor(
    private lancamentosService: LancamentosService,
    private consolidadoService: ConsolidadoService,
    private fb: FormBuilder
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      tipo: [TipoLancamento.Credito, Validators.required],
      valor: [null, [Validators.required, Validators.min(0.01)]],
      descricao: ['', [Validators.required, Validators.maxLength(255)]],
      data: [this.hoje(), Validators.required],
      meioLancamento: [null]
    });

    this.carregarDados();
  }

  carregarDados(): void {
    this.carregarConsolidadoHoje();
    this.carregarLancamentos();
    this.carregarHistorico();
  }

  carregarConsolidadoHoje(): void {
    this.carregandoConsolidado.set(true);
    this.consolidadoService.obterPorData(this.hoje())
      .pipe(catchError(() => of(null)))
      .subscribe(dados => {
        this.consolidadoHoje.set(dados);
        this.carregandoConsolidado.set(false);
      });
  }

  carregarLancamentos(): void {
    this.carregandoLancamentos.set(true);
    this.lancamentosService.listarPorData(this.hoje())
      .pipe(catchError(() => of([])))
      .subscribe(lista => {
        this.lancamentos.set(lista);
        this.carregandoLancamentos.set(false);
      });
  }

  carregarHistorico(): void {
    this.carregandoHistorico.set(true);
    // Busca os últimos 7 dias
    const datas = Array.from({ length: 7 }, (_, i) => {
      const d = new Date();
      d.setDate(d.getDate() - i);
      return d.toISOString().split('T')[0];
    });

    const requisicoes = datas.map(data =>
      this.consolidadoService.obterPorData(data).pipe(catchError(() => of(null)))
    );

    forkJoin(requisicoes).subscribe(resultados => {
      const validos = resultados
        .filter((r): r is ConsolidadoDiario => r !== null)
        .sort((a, b) => a.data.localeCompare(b.data));
      this.historico.set(validos);
      this.carregandoHistorico.set(false);
    });
  }

  submeterLancamento(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.salvandoLancamento.set(true);
    this.mensagemSucesso.set('');
    this.mensagemErro.set('');

    this.lancamentosService.registrar(this.form.value).subscribe({
      next: () => {
        this.mensagemSucesso.set('Lançamento registrado com sucesso!');
        this.form.reset({
          tipo: TipoLancamento.Credito,
          data: this.hoje()
        });
        this.salvandoLancamento.set(false);
        // Recarrega dados após novo lançamento
        setTimeout(() => {
          this.carregarDados();
          this.mensagemSucesso.set('');
        }, 1500);
      },
      error: (err) => {
        this.mensagemErro.set(err?.error?.message ?? 'Erro ao registrar lançamento.');
        this.salvandoLancamento.set(false);
      }
    });
  }

  formatarData(data: string): string {
    const [ano, mes, dia] = data.split('-');
    return `${dia}/${mes}/${ano}`;
  }

  barraProgresso(valor: number, total: number): number {
    if (!total) return 0;
    return Math.min((valor / total) * 100, 100);
  }

  maxHistorico(): number {
    return Math.max(...this.historico().map(h => Math.abs(h.saldoFinal)), 1);
  }
}
