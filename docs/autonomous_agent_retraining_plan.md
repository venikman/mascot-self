# Autonomous Agent Retraining Strategy

This document captures the concrete plan for implementing the OpenAI Cookbook
“Autonomous Agent Retraining” pattern inside the multi-agent workflow project.
It covers the shared building blocks plus the specifics for both LM Studio
(`LmStudio` provider) and Azure OpenAI (`AzureOpenAI` provider).

---

## 1. Objectives
- **Closed-loop improvement**: continuously run workflows on curated tasks, score
  them, and promote only high-signal conversations into a training set.
- **Traceability**: persist prompts, tool calls, verification scores, recovery
  actions, latency, and cost for every workflow execution.
- **Safe deployment**: regressions are blocked through automated evaluations and
  staged rollouts.

---

## 2. Repository Touchpoints
| Area | File(s) | Planned change |
| --- | --- | --- |
| Configuration | `AgentLmLocal/Configuration/AgentConfiguration.cs` | Add dataset paths, storage settings, and dual deployment slot names. |
| Telemetry | `AgentLmLocal/Services/RunTracker.cs` + new `RunArchiveService` | Persist full transcripts, verification scores, and promotion flags to durable storage. |
| Agents | `AgentLmLocal/Agents/*` | Emit richer metadata (token usage, retries, evaluator outcomes) and add a `PromotionGuardAgent`. |
| Workflow runtime | `AgentLmLocal/Workflow/*` | Surface events needed for dataset export and evaluation dashboards. |
| Tooling | new `scripts/` + `docs/` | Automation for dataset export, local training, Azure fine-tuning, regression playback. |

---

## 3. Implementation Phases
1. **Observability & Storage**
   - Extend `RunTracker` to capture every `WorkflowEvent`, the originating task,
     intermediate prompts, outputs, verification scores, recovery outcomes, and
     token/cost stats.
   - Store runs locally (SQLite/JSONL) and optionally mirror them to Azure
     Storage/Table/Cosmos when `AzureOpenAI` is selected.
   - Provide query/export APIs for downstream services.

2. **Automatic Evaluators**
   - Keep `VerifierAgent` as the primary judge but add a dedicated
     `PromotionGuardAgent` that reviews full transcripts and emits
     `promote | reject | needs_human` plus rationale.
   - Encode promotion gates (minimum score, zero escalations, runtime budget) in
     configuration so we can tune thresholds per provider/environment.

3. **Dataset Builder**
   - Introduce a `DatasetCurationHostedService` (background worker) that:
     1. Pulls promotable runs from storage.
     2. Normalizes them to OpenAI/LLM fine-tune JSONL format with explicit
        `messages`, tool traces, and evaluation labels.
     3. Emits versioned artifacts under `data/autoretrain/{timestamp}` along with
        a manifest capturing source model, agent version, and commit SHA.
   - Supports both training and eval splits.

4. **Continuous Task Generator**
   - Maintain a catalog of evaluation prompts (YAML/JSON) plus a
     `SelfPlayRunner` that schedules `/run` calls against those scenarios so the
     system continuously generates fresh data.
   - Tag scenarios with categories to enable per-domain analytics.

5. **Regression & Promotion Gate**
   - Build a harness that replays the evaluation catalog through both the
     incumbent and candidate models, measuring verification pass rates,
     average quality score, recovery frequency, latency, and cost.
   - Promotion occurs only when the candidate clears configurable lifts over the
     baseline; otherwise we stay on the previous deployment.

---

## 4. Local / LM Studio Strategy
1. **Data Location**
   - Store telemetry in `AgentLmLocal/data/runs.db` (SQLite) plus export JSONL
     snapshots through `RunArchiveService`.
2. **Training Stack**
   - Use Hugging Face `transformers` with QLoRA/LoRA to fine-tune the curated
     dataset. Emit adapter weights (e.g., `models/self-evolve/v1`).
3. **Model Hosting & Swap**
   - Load the new weights in LM Studio, expose them via the existing
     OpenAI-compatible endpoint, update `AgentConfiguration.ModelId`, and
     restart the service once regression tests pass.
4. **Automation**
   - Provide `scripts/train_local.sh` to orchestrate dataset export, training,
     evaluation runs, and configuration updates. Keep previous checkpoints for
     rollback.

---

## 5. Azure OpenAI Strategy
1. **Storage & Pipelines**
   - Mirror telemetry to Azure Storage (Blob + Table) or Cosmos DB for easy
     querying. Optionally index transcripts in Azure AI Search.
   - Use Azure ML or GitHub Actions to trigger dataset export and kick off
     fine-tune jobs.
2. **Fine-Tuning**
   - Convert the curated dataset to the Azure OpenAI JSONL schema and call the
     fine-tuning API (`az openai model fine-tune create`). Track job IDs,
     monitor status, and capture resulting model identifiers.
3. **Deployment Slots**
   - Maintain two deployment names in configuration: `Primary` and `Canary`.
     After training, deploy the fine-tuned model into the canary slot and route
     automated evaluation traffic there.
4. **Canary Analysis & Promotion**
   - Compare canary metrics vs. primary. If improvements hold, swap the slots
     (or flip env vars) and restart the app. Keep feature flags for instant
     rollback.
5. **Cost & Governance**
   - Persist token usage (from `ChatCompletionsUsage`) per run; feed into Azure
     Monitor dashboards. Prefer Managed Identity over API keys when available.

---

## 6. Immediate Next Steps
1. Decide on the telemetry store (SQLite vs. Azure Table/Cosmos) and update
   `RunTracker` accordingly.
2. Implement the promotion evaluator agent and dataset curation background
   service.
3. Create automation scripts/pipelines for local training and Azure fine-tuning.
4. Build the regression harness plus scenario catalog, then run an end-to-end
   dry run against LM Studio before enabling the Azure loop.
