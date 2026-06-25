# _[Review Title]_

## 1. Introduction

### 1.1 Purpose

This document records the formal review of a set of project files.

### 1.2 Scope

_[Describe the scope of this review, including which project, release, or change is being reviewed.]_

### 1.3 Outcomes

Each check must be recorded with one of the following outcomes:

| Outcome | Meaning |
| :------ | :------ |
| Pass | The check was performed and the criterion is satisfied |
| Fail | The check was performed and the criterion is not satisfied |
| N/A | The check does not apply; justification is required |

### 1.4 Review Details

| Field | Value |
| :---- | :---- |
| Project | _[Project name]_ |
| Review ID | _[Review identifier]_ |
| Review Title | _[Review title]_ |
| Fingerprint | _[Fingerprint of the complete file set]_ |
| Review Date | _[YYYY-MM-DD]_ |

### 1.5 Reviewers

| Name | Role | Organization | Signature | Date |
| :--- | :--- | :----------- | :-------- | :--- |
| _[Reviewer name]_ | _[Role]_ | _[Organization]_ | _[Signature]_ | _[YYYY-MM-DD]_ |
| _[Reviewer name]_ | _[Role]_ | _[Organization]_ | _[Signature]_ | _[YYYY-MM-DD]_ |

### 1.6 Files Under Review

| File |
| :--- |
| _[filename]_ |

---

## 2. Review Checklist

### 2.1 Requirements Checks

**Applicable:** Yes / No

_Skip this section if the review contains no requirements files. If not applicable,
record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| REQ-01 | All requirements have a unique identifier | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQ-02 | All requirements are unambiguous (only one valid interpretation) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQ-03 | All requirements are testable (compliance can be demonstrated by a test) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQ-04 | All requirements are consistent (no requirement contradicts another) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQ-05 | All requirements are complete (no TBDs, undefined terms, or missing information) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQ-06 | All requirements are verifiable (can be objectively confirmed as met or not met) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQ-07 | No compound requirements are present (each requirement expresses a single testable criterion) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQ-08 | No requirements are missing (all expected behaviors and constraints are specified) | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.2 Design Documentation Checks

**Applicable:** Yes / No

_Skip this section if the review contains no design documentation files. If not applicable,
record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| DES-01 | Design documentation clearly describes the purpose of the component or feature | Pass / Fail / N/A | _Required if Fail or N/A_ |
| DES-02 | Design documentation covers the necessary implementation details | Pass / Fail / N/A | _Required if Fail or N/A_ |
| DES-03 | Design documentation describes how the code is interfaced (APIs, inputs, outputs) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| DES-04 | Design documentation describes the expected normal operation | Pass / Fail / N/A | _Required if Fail or N/A_ |
| DES-05 | Design documentation describes the expected error handling | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.3 Technical Documentation Checks

**Applicable:** Yes / No

_Skip this section if the review contains no general technical documentation files (e.g., user guides,
API references, README files, release notes). If not applicable, record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| DOC-01 | Documentation is free of technical inaccuracies | Pass / Fail / N/A | _Required if Fail or N/A_ |
| DOC-02 | Documentation is consistent with the current implementation and requirements | Pass / Fail / N/A | _Required if Fail or N/A_ |
| DOC-03 | All referenced external documents and dependencies are correctly identified | Pass / Fail / N/A | _Required if Fail or N/A_ |
| DOC-04 | Documentation is free of spelling and grammar errors | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.4 Code Checks

**Applicable:** Yes / No

_Skip this section if the review contains no source code files. If not applicable,
record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| CODE-01 | Code conforms to the project coding standards and style guide | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODE-02 | No obvious resource leaks are present (file handles, connections, memory) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODE-03 | No hardcoded values are present that should be configurable | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODE-04 | Each unit or function has a single, well-defined responsibility | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODE-05 | Code is written at the appropriate level of abstraction | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODE-06 | Code has an appropriate amount of extensibility for its context | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.5 Logic Error Checks

**Applicable:** Yes / No

_Skip this section if the review contains no source code files. If not applicable,
record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| LOGIC-01 | Code does only what is intended (no unintended side effects or behaviors) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| LOGIC-02 | All significant inputs and boundary conditions are handled correctly | Pass / Fail / N/A | _Required if Fail or N/A_ |
| LOGIC-03 | Concurrency and threading concerns are identified and addressed | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.6 Error Handling & Logging Checks

**Applicable:** Yes / No

_Skip this section if the review contains no source code files. If not applicable,
record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| ERR-01 | Error handling follows the approach described in the design documentation | Pass / Fail / N/A | _Required if Fail or N/A_ |
| ERR-02 | The logging volume and level of detail are appropriate | Pass / Fail / N/A | _Required if Fail or N/A_ |
| ERR-03 | Error messages are user-friendly and actionable | Pass / Fail / N/A | _Required if Fail or N/A_ |
| ERR-04 | Error messages and log entries do not leak sensitive data | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.7 Usability / Accessibility Checks

**Applicable:** Yes / No

_Skip this section if usability and accessibility are not relevant to the files under review.
If not applicable, record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| USE-01 | The feature or API is easy to use correctly | Pass / Fail / N/A | _Required if Fail or N/A_ |
| USE-02 | All public APIs are well documented | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.8 Test Checks

**Applicable:** Yes / No

_Skip this section if the review contains no test code files. If not applicable,
record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| TEST-01 | Tests cover expected (happy-path) behavior | Pass / Fail / N/A | _Required if Fail or N/A_ |
| TEST-02 | Tests cover error conditions and boundary cases | Pass / Fail / N/A | _Required if Fail or N/A_ |
| TEST-03 | Tests are independent and repeatable (no shared mutable state, no ordering dependency) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| TEST-04 | Test names clearly describe the behavior being verified | Pass / Fail / N/A | _Required if Fail or N/A_ |
| TEST-05 | New test cases are added for new functionality or defect fixes | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.9 Security Checks

**Applicable:** Yes / No

_Skip this section if the review contains no source code files or if security concerns are not
relevant to the files under review. If not applicable, record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| SEC-01 | No obvious security vulnerabilities are present (e.g., injection flaws, hardcoded credentials) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| SEC-02 | Authentication and authorization are handled correctly (see design documentation) | Pass / Fail / N/A | _Required if Fail or N/A_ |
| SEC-03 | Sensitive data is stored and transmitted securely | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.10 Code Readability Checks

**Applicable:** Yes / No

_Skip this section if the review contains no source code files. If not applicable,
record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| READ-01 | Code is easy to understand | Pass / Fail / N/A | _Required if Fail or N/A_ |
| READ-02 | Methods and functions are small enough to be easily understood | Pass / Fail / N/A | _Required if Fail or N/A_ |
| READ-03 | Symbols (variables, functions, classes) are well named | Pass / Fail / N/A | _Required if Fail or N/A_ |
| READ-04 | Code is located in the correct place in the codebase | Pass / Fail / N/A | _Required if Fail or N/A_ |
| READ-05 | Flow of control can be easily followed | Pass / Fail / N/A | _Required if Fail or N/A_ |
| READ-06 | Data flow is understandable | Pass / Fail / N/A | _Required if Fail or N/A_ |
| READ-07 | Comments are provided where the code is non-obvious | Pass / Fail / N/A | _Required if Fail or N/A_ |
| READ-08 | No debug artifacts or commented-out code have been left in the codebase | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.11 Requirements vs Documentation Checks

**Applicable:** Yes / No

_Skip this section if the review contains no requirements files or no general technical documentation
files. If not applicable, record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| REQDOC-01 | All reviewed requirements are addressed in the general technical documentation | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQDOC-02 | No reviewed requirement is contradicted by the general technical documentation | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.12 Requirements vs Implementation Checks

**Applicable:** Yes / No

_Skip this section if the review contains no requirements files or no source code
files. If not applicable, record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| REQIMP-01 | All requirements under review are addressed by the implementation | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQIMP-02 | No requirement is contradicted by the implementation | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.13 Requirements vs Testing Checks

**Applicable:** Yes / No

_Skip this section if the review contains no requirements files or no test code files.
If not applicable, record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| REQTEST-01 | Every requirement under review is covered by at least one test | Pass / Fail / N/A | _Required if Fail or N/A_ |
| REQTEST-02 | Tests verify the behavior described in each requirement | Pass / Fail / N/A | _Required if Fail or N/A_ |

### 2.14 Code vs Design Documentation Checks

**Applicable:** Yes / No

_Skip this section if the review contains no source code files or no design documentation
files. If not applicable, record the reason here:_

| # | Check | Outcome | Justification |
| :-- | :---- | :------ | :------------ |
| CODEDOC-01 | The code correctly implements the design documentation | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODEDOC-02 | All public APIs and interfaces are documented in the design documentation | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODEDOC-03 | Non-obvious algorithms and significant design decisions are explained in the design documentation | Pass / Fail / N/A | _Required if Fail or N/A_ |
| CODEDOC-04 | No important code details are missing from the design documentation | Pass / Fail / N/A | _Required if Fail or N/A_ |

---

## 3. Conclusion

### 3.1 Summary of Findings

_List any checks recorded as Fail, and any observations that do not constitute a failure
but should be noted for the project record:_

| # | Check | Finding |
| :-- | :---- | :------ |
| _[check ID]_ | _[check description]_ | _[describe the finding]_ |

### 3.2 Overall Outcome

**Overall Outcome:** Pass / Fail

_State the basis for the overall outcome, including any conditions or follow-up actions
required before the review can be considered approved:_
