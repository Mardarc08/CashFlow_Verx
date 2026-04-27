import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Lancamento, RegistrarLancamentoRequest, RegistrarLancamentoResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class LancamentosService {
  private readonly baseUrl = `${environment.lancamentosApiUrl}/api/lancamentos`;

  constructor(private http: HttpClient) {}

  registrar(request: RegistrarLancamentoRequest): Observable<RegistrarLancamentoResponse> {
    return this.http.post<RegistrarLancamentoResponse>(this.baseUrl, request);
  }

  listarPorData(data: string): Observable<Lancamento[]> {
    const params = new HttpParams().set('data', data);
    return this.http.get<Lancamento[]>(this.baseUrl, { params });
  }
}
