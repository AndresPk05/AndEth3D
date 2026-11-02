# Pass an approved base digest and an exact Debian package version at build time.
ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG PRUSA_PACKAGE_VERSION
RUN test -n "$PRUSA_PACKAGE_VERSION" && apt-get update && apt-get install -y --no-install-recommends \
    "prusa-slicer=$PRUSA_PACKAGE_VERSION" python3 ca-certificates && rm -rf /var/lib/apt/lists/*
COPY worker.py /opt/worker.py
USER 65532:65532
ENV HOME=/tmp LC_ALL=C
ENTRYPOINT ["python3", "/opt/worker.py"]
