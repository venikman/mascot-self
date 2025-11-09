# 🧠 Agentic System Design under FPF + Agentic Design Patterns (Gulli 2025)
## 1. Overview
This document integrates **Agentic Design Patterns** (Gulli 2025 Springer) with the **First Principles Framework (FPF v6)** to define a seven-agent architecture designed around context engineering and bounded contexts (`U.BoundedContext`) for holonic agents (`U.System ⊑ U.Holon`).  
Each agent operates within its context contract—scope, tools, memory, metrics, guardrails—and publishes its outputs via a bounded `U.ContextBridge`.  
The architecture follows the AgentVerse loop (Recruit → Deliberate → Do → Audit) mapped onto FPF’s canonical reasoning cycle (Explore → Shape → Evidence → Operate).

---

## 2. Agent Definitions + Context Contracts

| # | Agent | Purpose & Core Patterns | Context Components (per FPF §A.1.1) |
|:-:|-------|--------------------------|--------------------------------------|
| **1** | **Orchestrator / Router** | Routes tasks and allocates agents; uses **Routing** and **Auto-Flow** patterns (pp. 34–47 in Gulli). | `Glossary`: task, policy, budget • `Invariants`: SoD and budget caps • `Roles`: Coordinator, Scheduler • `Bridges`: to Planner and Safety contexts. |
| **2** | **Planner** | Decomposes missions into steps; applies **Prompt Chaining** and structured output (pp. 21–33). | `Glossary`: plan, step, acceptance criteria • `Invariants`: no recursive chains > 4 depth • `Roles`: Strategist, Planner • `Bridge`: Orchestrator ↔ Contextor. |
| **3** | **Contextor / Librarian** | Builds context pack via RAG pattern (pp. 212 ff.) and FPF `U.CN-Spec` (Units, Chart, Normalization). | `Glossary`: context pack, descriptor, source • `Invariants`: unit + polarity declared • `Roles`: Retriever, Indexer • `Bridge`: to Researcher and Planner. |
| **4** | **Researcher / Tool-User** | Uses **Tool Use** and A2A patterns (pp. 79–81 + Ch. 15); executes calls via MCP schemas. | `Glossary`: tool, API scope • `Invariants`: rate limit ≤ R budget • `Roles`: Tool-User, Fetcher • `Bridge`: to Contextor and Synthesizer. |
| **5** | **Synthesizer / Writer** | Generates artifacts from context packs; uses **Chaining + Struct Out** (pp. 354–357). | `Glossary`: artifact, persona • `Invariants`: structured schema validates • `Roles`: Author, Composer • `Bridge`: to Critic. |
| **6** | **Critic / Reflector** | Applies **Reflection / Self-Correction** (pp. 426 ff.); detects contradictions & improves output. | `Glossary`: issue, edit • `Invariants`: no self-review violations (SoD) • `Roles`: Reviewer, Auditor • `Bridge`: to Safety and Orchestrator. |
| **7** | **Safety / Evaluator** | Enforces guardrails, rollbacks, telemetry (pp. 300–301). | `Glossary`: rule, policy, alert • `Invariants`: CC-A0-1 ↔ A0-12 conformance • `Roles`: Guard, Assurer • `Bridge`: logs → EvidenceGraph (B.3). |

---

## 3. Multi-Context System Design

### 3.1 Bounded Contexts (U.BoundedContext)

| Context | Description | Key Invariants & Governance |
|----------|--------------|------------------------------|
| **U.Orchestration** | Task graph management, budgeting, supervision loop (E/E-LOG policy). | No orphan tasks; delegation depth ≤ 2; budget ledger per mission. |
| **U.Research** | Information retrieval & tool use under CN-Specs. | Units and polarity must match CG-Frame; no cross-context data without Bridge. |
| **U.Authoring** | Artifact creation and review context. | Schema validation required per MM-CHR; SoD Reviewer ≠ Author. |
| **U.Assurance** | Observability, rollback, DRR logging. | Every claim links to SCR/RSCR evidence; no unanchored trust statements. |

---

### 3.2 Agent Interaction Flow

```
Orchestrator → Planner → Contextor → Researcher → Synthesizer → Critic → Safety → Orchestrator
```

Each arrow is a Bridge with declared loss (CL penalty to R).  
Contextors operate under CG-Frames; Safety writes to EvidenceGraph.

---

## 4. Context Engineering Best Practices (merged guidelines)

1. **Structured Context Pack** = { mission, role-instruction, retrieved_docs[], tool_outputs[], implicit_data, schema, constraints, safety_flags }. Every agent receives and returns this object (JSON schema typed).  
2. **FPF CN-Spec** declares units, chart, normalization, comparability mode, SoD, DRR link, Bridge-only reuse, and assurance lanes.  
3. **Observability:** log inputs/outputs, reasoning summaries, confidence; checkpoint states (T-1..T-3).  
4. **A2A communication:** typed messages with shared ontology; use MCP schemas for tool intents.  
5. **Open↔Closed World discipline:** exploration (OWA) inside design; decisions (CWA) inside bounded contexts.  
6. **Safety Controls:** SoD enforced by RoleAssignment; guardrails & RoC policies bounded by autonomy budget (E.16).  
7. **Evidence:** Every artifact → EvidenceGraph link with ClaimRef and Assurance Level (B.3.3).  

---

## 5. Performance Estimation (Fermi Pass)

| Metric | 90 % Interval | Dominant Unknowns |
|---------|---------------|-------------------|
| End-to-End latency | 6–12 s | Model latency tails, cache hit rate |
| Tokens per step | ≈ 2–4 × 10³ | Prompt size, tool output size |
| Data per task | 0.2–1.0 MB | Document retrieval variance |

---

## 6. FPF Cycle Alignment

| FPF Stage | Agentic Phase | Observable Artifact |
|------------|---------------|---------------------|
| **Explore** | Planner + Contextor | Context pack, abductive hypotheses |
| **Shape** | Synthesizer + Critic | Draft artifact + issue list |
| **Evidence** | Safety + Assurance | Validated logs → EvidenceGraph |
| **Operate** | Orchestrator + Researcher | Closed loop execution and telemetry |

---

## 7. Testing Hypotheses (Disconfirmable)

| ID | Hypothesis | Metric & Test |
|:--:|-------------|----------------|
| H1 | Context packs cut turns ≥ 25 % | A/B vs no-context baseline on 200 tasks (pp. 354–357). |
| H2 | CN-Specs halve metric drift | Synthetic rescaling test of CG-Frames. |
| H3 | Checkpoint/rollback reduces MTTR 50 % | Inject tool failure → recovery time test. |
| H4 | Critic pass lowers factual errors ≥ 30 % | Human evaluation of outputs (pp. 426 ff.). |

---

## 8. Risks & Mitigations

| Risk | Mitigation |
|-------|-------------|
| Context bloat → token waste | Trim per role; TTL context elements 30 min. |
| Stale implicit data | Refresh loop with telemetry timestamp < 24 h. |
| Cross-context misuse | Only Bridge via CL-penalized channels. |
| Over-routing latency | Heuristic: route depth ≤ 2 per mission. |

---

## 9. Next Steps (Executable Roadmap)

1. **Author CN-Spec v0** for U.Research — declare unit, chart, SoD, DRR.  
2. **Implement Contextor + Researcher** pair in LangGraph or Google ADK demo.  
3. **Add Critic + Safety** loops with checkpoint / rollback telemetry.  
4. **Run H1–H4 benchmarks;** record results in EvidenceGraph and UTS row.  

---

## 10. Source References (Access 2025-11-09)

- **Antonio Gulli (2025)** *Agentic Design Patterns: A Hands-On Guide to Building Intelligent Systems* (Springer Nature).  
- **Levenchuk et al. (2025)** *First Principles Framework — Core Conceptual Specification (holonic v6)* (September 2025).  

---

*Composed for export and integration with FPF tooling (UTS / DRR pipeline).*
