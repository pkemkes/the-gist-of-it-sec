# #!/bin/bash
set -e

mariadb --user=root --password="$MARIADB_ROOT_PASSWORD" <<-EOSQL
  CREATE USER IF NOT EXISTS '${DB_GISTSBOT_USERNAME}'@'%' IDENTIFIED BY '${DB_GISTSBOT_PASSWORD}';
  GRANT SELECT, INSERT, UPDATE, DELETE on thegistofitsec.gists TO '${DB_GISTSBOT_USERNAME}'@'%';
  GRANT SELECT, INSERT, UPDATE, DELETE on thegistofitsec.feeds TO '${DB_GISTSBOT_USERNAME}'@'%';
  GRANT SELECT, INSERT, UPDATE, DELETE on thegistofitsec.search_results TO '${DB_GISTSBOT_USERNAME}'@'%';
  GRANT SELECT, INSERT, UPDATE, DELETE on thegistofitsec.recaps_daily TO '${DB_GISTSBOT_USERNAME}'@'%';
  GRANT SELECT, INSERT, UPDATE, DELETE on thegistofitsec.recaps_weekly TO '${DB_GISTSBOT_USERNAME}'@'%';

  CREATE USER IF NOT EXISTS '${DB_RESTAPI_USERNAME}'@'%' IDENTIFIED BY '${DB_RESTAPI_PASSWORD}';
  GRANT SELECT on thegistofitsec.gists TO '${DB_RESTAPI_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.feeds TO '${DB_RESTAPI_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.search_results TO '${DB_RESTAPI_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.recaps_daily TO '${DB_RESTAPI_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.recaps_weekly TO '${DB_RESTAPI_USERNAME}'@'%';
  
  CREATE USER IF NOT EXISTS '${DB_TELEGRAMBOT_USERNAME}'@'%' IDENTIFIED BY '${DB_TELEGRAMBOT_PASSWORD}';
  GRANT SELECT on thegistofitsec.gists TO '${DB_TELEGRAMBOT_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.feeds TO '${DB_TELEGRAMBOT_USERNAME}'@'%';
  GRANT SELECT, INSERT, UPDATE, DELETE on thegistofitsec.chats TO '${DB_TELEGRAMBOT_USERNAME}'@'%';

  CREATE USER IF NOT EXISTS '${DB_CLEANUPBOT_USERNAME}'@'%' IDENTIFIED BY '${DB_CLEANUPBOT_PASSWORD}';
  GRANT SELECT, INSERT, UPDATE, DELETE on thegistofitsec.gists TO '${DB_CLEANUPBOT_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.feeds TO '${DB_CLEANUPBOT_USERNAME}'@'%';
  
  CREATE USER IF NOT EXISTS '${DB_GRAFANA_USERNAME}'@'%' IDENTIFIED BY '${DB_GRAFANA_PASSWORD}';
  GRANT SELECT on thegistofitsec.gists TO '${DB_GRAFANA_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.feeds TO '${DB_GRAFANA_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.search_results TO '${DB_GRAFANA_USERNAME}'@'%';
  GRANT SELECT on thegistofitsec.chats TO '${DB_GRAFANA_USERNAME}'@'%';

  FLUSH PRIVILEGES;
EOSQL