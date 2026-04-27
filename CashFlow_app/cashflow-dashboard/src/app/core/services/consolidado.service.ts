import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ConsolidadoDiario } from '../models';

@Injectable({ providedIn: 'root' })
export class ConsolidadoService {
  private readonly baseUrl = `${environment.consolidadoApiUrl}/api/consolidado`;

  constructor(private http: HttpClient) {}

  obterPorData(data: string): Observable<ConsolidadoDiario> {
    return this.http.get<ConsolidadoDiario>(`${this.baseUrl}/${data}`);
  }

  obterHistorico(dataInicio: string, dataFim: string): Observable<ConsolidadoDiario[]> {
    // Busca os últimos N dias montando requisições paralelas via forkJoin no componente
    return this.http.get<ConsolidadoDiario[]>(`${this.baseUrl}?inicio=${dataInicio}&fim=${dataFim}`);
  }
}
