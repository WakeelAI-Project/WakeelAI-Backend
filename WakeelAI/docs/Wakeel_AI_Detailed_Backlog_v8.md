```markdown
# Wakeel AI - Detailed MVP Backlog (v8 - Migration Sequence Aligned)
**Tracks:** `[BE]` .NET 10 | `[FE]` React Web | `[MOB]` Flutter App | `[AI-NODE]` Node.js AI Service[cite: 17]

## Sprint 1 & 2: Foundations & Core HR Admin (Completed / Code-Aligned)
These stories are fully implemented in the .NET codebase and React web dashboard[cite: 17].
*   **Story 1.1-1.4:** Auth & RBAC `[Completed]`[cite: 17].
*   **Story 1.9 & 2.8:** Company Profile `[Completed]`[cite: 17].
*   **Story 2.4:** HR Accounts `[Completed]`[cite: 17].
*   **Story 2.5:** Department Management `[Completed]`[cite: 17].
*   **Story 2.6:** Employee Management `[Completed]`[cite: 17].
*   **Story 2.7:** Audit Logging `[Completed]`[cite: 17].

---

## Sprint 3: The Great AI Migration & M2M Auth (Days 10-14)
**Sprint Goal:** Move AI orchestration out of .NET into a standalone Node.js service. Establish Machine-to-Machine (M2M) authentication. Build the API Gateway routing for chat, internal context endpoints for data fetching, and the LangChain RAG pipeline in MongoDB.

### Story 3.1: AI Service Foundation & M2M Security
*   `[BE]` Implement `X-Internal-API-Key` middleware in .NET. Must secure all server-to-server endpoints (bypassing JWT)[cite: 17].
*   `[AI-NODE]` Scaffold the Node.js + Express service. Strictly JavaScript (ES Modules). No TypeScript, no BullMQ worker architectures allowed[cite: 17].
*   `[AI-NODE]` Setup Winston for centralized logging and Zod for runtime request validation[cite: 17].
*   `[AI-NODE]` Create `config/env.js` to validate required variables: `LLM_API_KEY`, `MONGODB_URI`, `WAKEEL_INTERNAL_API_KEY`, `WAKEEL_API_BASE_URL`. Fail process if missing[cite: 17].
*   `[AI-NODE]` Implement `GET /health` returning service status[cite: 17].

### Story 3.2: Internal Context Integrations (.NET Data Access)
*Note: Database migrations for upcoming tables are executed here to unblock the internal integration endpoints.*
*   `[BE]` **Create `DOCUMENT_TEMPLATE` and `GENERATED_DOCUMENT` EF Core migrations.** (Moved from Sprint 4 to unblock S3 endpoints).
*   `[BE]` Build `GET /api/ai/employee-context` (secured by `X-Internal-API-Key` + `X-User-Id` + `X-Company-Id` + `X-Role`). Returns exact schema in API Doc v5 §10.3[cite: 17].
*   `[BE]` Build `GET /api/ai/company-context` (same four headers). Returns exact schema in API Doc v5 §10.4. No policies field - RAG owns policy content exclusively[cite: 17].
*   `[BE]` Build `GET /api/ai/templates/active?documentType=` (same four headers)[cite: 17]. Must enforce the one-active-template-per-document-type-per-company rule[cite: 17].
*   `[BE]` Build `POST /api/documents/save` (same four headers). Request contract finalized in v5, §10.7[cite: 17].
*   `[AI-NODE]` `WakeelApiClient` module: configure outbound HTTP client with base URL and attach internal auth headers to every outgoing request[cite: 17].
*   `[AI-NODE]` Add `DocumentSaveRequestSchema` (Zod, `.strict()`) validating the outbound payload against the finalized field list before it is sent[cite: 17].

### Story 3.3: Chat History Gateway & Mongo Persistence
*   `[AI-NODE]` Provision MongoDB Atlas cluster. Create collections: `Conversations` and `Messages` (with nested `Citations`)[cite: 17].
*   `[AI-NODE]` Build `GET /api/ai/chat/history` to fetch a user's chat history from MongoDB, filtered by `companyId`, `userId`, and `conversationId`[cite: 17].
*   `[BE]` Refactor .NET `GET /chat/history` endpoint to act as an API Gateway: resolve claims, require `conversation_id`, proxy HTTP request to Node.js, and return payload[cite: 17].
*   `[BE]` Refactor .NET `POST /chat/ask` endpoint to act as an API Gateway, and own the `conversationId` lifecycle[cite: 17]. .NET mints/reuses `conversationId`, seamlessly routes request to Node.js[cite: 17].
*   `[BE]` Configure the .NET `HttpClient` with a strict minimum 60-second timeout for Node.js calls to prevent orphaned LLM generation tasks[cite: 17].

### Story 3.4: LLM Orchestration & RAG Pipeline (Node.js)
*   `[AI-NODE]` Create LangChain chat-model and embedding abstractions (`src/llm/chat-model.js`, `embeddings.js`)[cite: 17].
*   `[BE]` **Create `COMPANY_HANDBOOK` EF Core migration** to support handbook uploads.
*   `[AI-NODE]` Implement `POST /api/knowledge/ingest`. Validates Zod schema, splits text, generates embeddings, and stores in MongoDB[cite: 17].
*   `[BE]` Hook into the existing `POST /company/policy-document` endpoint. When the Owner uploads a Handbook PDF, .NET extracts raw text and forwards it (with internal headers) to Node.js ingestion endpoint[cite: 17].
*   `[AI-NODE]` Implement `searchKnowledge` retrieving chunks scoped strictly by `companyId` (for policy) or null (for public labor law)[cite: 17].

### Story 3.5: AI Skills & Tools Implementation
*   `[AI-NODE]` Build Skill & Tool Registries[cite: 17].
*   `[AI-NODE]` Calculator Skill: Implement deterministic HR calculations in pure JavaScript. Do not use LLM for arithmetic[cite: 17].
*   `[AI-NODE]` Document Generation Skill (scaffold): fetch context and active template, fill placeholders, call .NET `POST /api/documents/save`[cite: 17].
*   `[AI-NODE]` Leave Request Tool: Submit draft leave request calling standard .NET `POST /leave-requests` endpoint using `WakeelApiClient`[cite: 17].
*   `[AI-NODE]` Build Orchestrator (`intent-router.js`). Detect intent, ask for `missing_fields`, trigger tools, format `result_card`[cite: 17].

---

## Sprint 4: Feature Completion & Mobile Delivery (Days 15-19)
**Sprint Goal:** Complete the HR Dashboard document and leave workflows. Deliver the fully functional Flutter mobile application for employees.

### Story 4.1: Document Templates & Generation UI (HR)
*   `[FE]` Build the HR "Templates" page: list by type, activate/deactivate, and rich-text editor with `{{placeholders}}`[cite: 17].
*   `[FE]` Build the "Generate Document" AI chat flow. Render `missing_fields` as dynamic forms[cite: 17]. Show Draft Review screen[cite: 17].
*   `[FE]` Build "Finalize & Send" flow. Post-finalization, export HTML to PDF and trigger email[cite: 17].

### Story 4.2: HR Leave Approvals & Overviews
*   `[FE]` Build HR "Leave Approvals" page[cite: 17].
*   `[FE]` Build Audit Logs viewer table[cite: 17].
*   `[FE]` Build HR Overview cards[cite: 17].

### Story E1 & E2: Mobile Home & Profile
*   `[MOB]` Build Bottom Navigation Shell (Home, Chat, Leaves, Documents)[cite: 17].
*   `[MOB]` Build Profile Screen. Wire `GET /me`[cite: 17].
*   `[MOB]` Build Home Dashboard. Display Annual/Sick/Unpaid leave balance stat cards[cite: 17].

### Story E3 & E4: Mobile AI Chat & Voice
*   `[MOB]` Build Chat UI. Message list with user/AI bubbles, timestamps, and progressive typewriter reveal. Must persist and resend `conversation_id`[cite: 17].
*   `[MOB]` Render `sources[]/citations` as distinct UI chips beneath AI replies[cite: 17].
*   `[MOB]` Render `missing_fields` as inline dynamic forms[cite: 17].
*   `[MOB]` Implement Voice Input (STT). Transcription lands in composer for review (never auto-send)[cite: 17].

### Story E5 & E6: Mobile Leave Requests & Documents
*   `[MOB]` Build "My Leave Requests" screen[cite: 17].
*   `[MOB]` Build chat-driven leave flow. Render `leave_draft` result card with inline Submit/Cancel actions[cite: 17]. Ensure Sick leave forces medical report PDF/image upload[cite: 17].
*   `[MOB]` Build "My Documents" screen[cite: 17].
*   `[MOB]` Build Document detail. In-app PDF viewer[cite: 17]. Drafts show "Being reviewed by HR"[cite: 17]. Finalized docs allow local device download[cite: 17].

### Story E7: Mobile Settings & Localization
*   `[MOB]` Build Settings screen: Language toggle (AR/EN), Theme selector, Logout[cite: 17].
*   `[MOB]` Ensure RTL/LTR layouts flip instantaneously and persist across app restarts[cite: 17].

---

## Sprint 5: Hardening, E2E Testing, Deployment & Demo (Days 20-23)
**Sprint Goal:** Stabilization only. Security sweeps, timeout handling, cross-service error resilience, and public deployment.

### Story 5.1: Cross-Service Stability & Security
*   `[AI-NODE]` Ensure graceful failure handling for distributed tasks (e.g. if .NET's `documents/save` endpoint returns 500, the AI gracefully informs the user)[cite: 17].
*   `[BE]` Input-validation sweep across every POST/PATCH endpoint ensuring strict 400 envelope returns[cite: 17].
*   `[BE]` `[AI-NODE]` Secrets sweep: verify JWT keys, Mongo URIs, LLM API Keys, and `WAKEEL_INTERNAL_API_KEY` are all managed via environment variables[cite: 17].

### Story 5.2: E2E Testing & AI Evaluation
*   `[BE]` Run integration suites on the test tenant[cite: 17].
*   `[AI-NODE]` AI Evaluation run: Assert 8/10 on Labor Law retrieval, exact-match asserts on JS Calculator functions, and proper Orchestrator intent routing[cite: 17].
*   `[MOB]` `[FE]` Execute manual QA matrix[cite: 17].

### Story 5.3: Deployment & Demo Seeding
*   `[BE]` `[AI-NODE]` Deploy .NET API and Node.js Express service to Azure/AWS[cite: 17]. Verify M2M connectivity[cite: 17].
*   `[BE]` Run the demo seed script[cite: 17].
*   `[FE]` Deploy React dashboard to static hosting[cite: 17].
*   `[MOB]` Produce release builds[cite: 17].
*   `[ALL]` Rehearse Demo Scenarios (Employee Voice Chat & HR Document Generation)[cite: 17].