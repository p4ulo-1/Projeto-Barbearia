#!/usr/bin/env bash
# ============================================================
# Backup do banco do Servia (F15.5)
#
# Executa pg_dump, calcula o checksum e remove backups antigos.
# O checksum permite verificar a integridade antes de restaurar.
#
# Uso:
#   ./scripts/backup.sh
#   DATABASE_URL=postgres://... ./scripts/backup.sh
#
# Agendamento (cron, diario as 3h):
#   0 3 * * * /caminho/scripts/backup.sh >> /var/log/servia-backup.log 2>&1
# ============================================================

set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-./backups}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
DATABASE_URL="${DATABASE_URL:-postgres://servia:servia_dev_password@localhost:5432/servia}"

TIMESTAMP="$(date +%Y%m%dT%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/servia-${TIMESTAMP}.sql"
CHECKSUM_FILE="${BACKUP_FILE}.sha256"

mkdir -p "${BACKUP_DIR}"

echo "Iniciando backup em ${BACKUP_FILE}"

# pg_dump com formato texto: legivel e restauravel com psql.
if ! pg_dump "${DATABASE_URL}" > "${BACKUP_FILE}"; then
  echo "ERRO: pg_dump falhou" >&2
  rm -f "${BACKUP_FILE}"
  exit 1
fi

# Verifica que o arquivo nao esta vazio: um backup vazio e pior que nenhum,
# porque da falsa seguranca.
if [ ! -s "${BACKUP_FILE}" ]; then
  echo "ERRO: backup vazio" >&2
  rm -f "${BACKUP_FILE}"
  exit 1
fi

# Checksum para verificacao de integridade na restauracao.
sha256sum "${BACKUP_FILE}" > "${CHECKSUM_FILE}"

SIZE="$(du -h "${BACKUP_FILE}" | cut -f1)"
echo "Backup concluido: ${SIZE}"

# Remove backups mais antigos que a retencao.
find "${BACKUP_DIR}" -name 'servia-*.sql' -mtime "+${RETENTION_DAYS}" -delete
find "${BACKUP_DIR}" -name 'servia-*.sql.sha256' -mtime "+${RETENTION_DAYS}" -delete

echo "Backups com mais de ${RETENTION_DAYS} dias removidos"
