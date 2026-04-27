#!/bin/bash
# entrypoint.sh — aguarda o SQL Server ficar pronto e executa o init.sql

echo "⏳ Aguardando SQL Server inicializar..."

# Espera até o sqlcmd conseguir conectar (máx 60s)
for i in $(seq 1 12); do
  /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P "$SA_PASSWORD" -Q "SELECT 1" -No 2>/dev/null
  if [ $? -eq 0 ]; then
    echo "✅ SQL Server pronto."
    break
  fi
  echo "   Tentativa $i/12 — aguardando 5s..."
  sleep 5
done

# Executa o script de inicialização
echo "🗄️ Criando bancos de dados..."
/opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P "$SA_PASSWORD" -i /docker-entrypoint-initdb/init.sql -No
echo "✅ Inicialização concluída."
