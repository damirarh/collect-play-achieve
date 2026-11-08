FROM docker.io/sbx/shell-docker-image:latest

ARG DOTNET_CHANNEL
ARG NODE_VERSION
ARG PNPM_VERSION
ARG ASPIRE_VERSION
ARG TARGETARCH

USER root

RUN apt-get update -qq && apt-get install -y --no-install-recommends \
    ca-certificates curl xz-utils libicu-dev libssl-dev libgssapi-krb5-2 zlib1g \
    && apt-get clean

COPY install.sh /usr/local/share/collect-play-achieve/install.sh
RUN bash /usr/local/share/collect-play-achieve/install.sh \
    "$DOTNET_CHANNEL" "$NODE_VERSION" "$PNPM_VERSION" "$ASPIRE_VERSION" "$TARGETARCH"

COPY environment.sh /etc/profile.d/collect-play-achieve-cpa.sh
