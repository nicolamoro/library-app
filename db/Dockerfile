FROM mcr.microsoft.com/mssql/server:2022-latest
USER root
COPY init/ /init/
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
USER mssql
ENTRYPOINT ["/entrypoint.sh"]
