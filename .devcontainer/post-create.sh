#!/usr/bin/env bash
# Pulls the two local models the samples use. Runs once when the container is created.
set -euo pipefail

endpoint="${OLLAMA_ENDPOINT:-http://ollama:11434}"

echo "Waiting for Ollama at $endpoint..."
until curl -sf "$endpoint/api/tags" > /dev/null; do sleep 2; done

for model in "${OLLAMA_VISION_MODEL:-qwen3.5:4b}" "${OLLAMA_EMBEDDING_MODEL:-embeddinggemma}"; do
  echo "Pulling $model (this takes a few minutes the first time)..."
  curl -sf "$endpoint/api/pull" -d "{\"model\": \"$model\", \"stream\": false}" > /dev/null
done

dotnet --version
echo "Ready. Try: dotnet run 01-extract.cs"
