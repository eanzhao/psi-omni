# Multi-Round Agent Design

## 1. Root Agent as Session Container
- The **root agent** (no parent) is the unique entry and identifier for a user session.
- All child agents (Orchestrator/Specialized) belong to this session and form a tree rooted at the root agent.
- All user follow-up messages are routed to the root agent, which decides how to dispatch, cancel, supplement, or create child agents.

## 2. Multi-Round Message Handling

### a. User Follow-Up Decision Branches
1. **Cancel Child Agent(s)**
   - User can specify to cancel certain child agents (e.g., revoke a branch task).
   - Root agent must support sending a "cancel" event to the specified child agent(s), recursively canceling all descendants.
2. **Provide Additional Information**
   - When the user provides more info, the root agent decides which child agent(s) should receive it (e.g., context supplement, instruction correction).
   - Use event mechanism to inject info into the relevant agent's context or chat history.
3. **Create More Agents**
   - For new user needs, the root agent can create new child agents and assign new tasks.

### b. Concurrency & Consistency
- If a specialized agent is in the middle of a tool call when a follow-up arrives:
  - **Queue**: Enqueue the message, process after tool call completes.
  - **Interrupt**: If tool call supports cancellation, send an interrupt signal.
  - **Parallel**: If safe, process in parallel with state isolation.

## 3. State & Session Tracking
- **SubTask Tracking**: Only track unfinished (pending/delegated) subtasks; completed tasks are archived in chat history.
- **Session Identifier**:
  - Optionally add a `sessionId` to all agents for cross-tree management.
  - Alternatively, use the root agent id as the session id; all interactions are routed via the root agent.

## 4. Structural Recommendations
- **Event-Driven**: All cancel/supplement/create actions are dispatched via events for decoupling and extensibility.
- **Tree Agent Structure**: Each agent records parentId/childIds; the root node is the session.
- **Multi-Round History**: All key actions, messages, and results are aggregated in the root agent's chat history for traceability.
- **Concurrency Safety**: If supporting tool call interruption/concurrency, design per-agent task queues and state machines.

## 5. Resonance Keywords
- "Root agent = session"
- "Event-driven multi-round decision"
- "Tree agent structure"
- "Track unfinished tasks, archive completed"
- "Concurrency safety and consistency"
