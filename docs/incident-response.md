# CycloneDX .NET Incident Response Checklist

This document is a checklist to help you through the _Incident Response Process_ (IRP) for a security vulnerability reported in CycloneDX .NET. 

---
## 📍 Introduction

A security vulnerability in CycloneDX .NET has been reported. This report might come through one of two broad categories:

- **Privately disclosed reports** — via GitHub Security Advisory, a direct email to the maintainers, or another trusted communication channel. These are preferred and give us time to assess and resolve issues before the public is made aware.
    
- **Public disclosures** — via an open GitHub issue, a discussion thread, or a post on public platforms like Twitter or Mastodon. These are suboptimal, as they expose the vulnerability before it has been confirmed or patched, and require an accelerated response.
    
Regardless of how the issue reaches us, this document outlines the standardized approach we take to triage, assess, and respond to such incidents.

This checklist should be copied into a privately created [security advisory](https://github.com/CycloneDX/cyclonedx-dotnet/security/advisories) as soon as a potentially valid report is received - even if it’s eventually triaged out. Capturing the triage process helps ensure we maintain a record of decisions made, preserve context for future reference, and support any retrospective or comparative analysis down the line.

---

## Phase 1 - Triage

### ✅ Tasks
_Things that should be completed before moving on_

> [!TIP]
> Take detailed notes on everything you learn during triage. Even discarded details may prove valuable later during investigation, review, or future incidents.

Tasks that should be completed before moving on to mitigation if any vulnerabilities are confirmed.

- [ ] If the report came via a public GitHub issue, delete the issue to prevent accidental exposure.
- [ ] If not originally reported via GitHub Security Advisory, create a private advisory draft to track the issue internally.
- [ ]  Review the report and reproduce the vulnerability
	- [ ] Request missing details from reporter or ask for a minimal PoC/exploit if needed
- [ ] Understand vulnerability
- [ ] Check if the issue aligns with or violates the project's threat model (if available).
- [ ] Determine severity
	- [ ] Use a recognized severity scoring framework such as CVSS or GitHub’s security advisory severity levels.
	- [ ] Consider the impact to confidentiality, integrity, and availability (CIA).
	- [ ] Factor in ease of exploitation, required privileges, user interaction, and affected scope.
- [ ] Decide if case will become an investigation. Either:
	- [ ] Dismiss lead as not-actionable and apply `not actionable` label
	- [ ] Move on with `Phase 2 - Mitigation`


> 📘 **Info:**  
> Not sure if the report is actionable? Use these criteria to guide your decision:
>
> * **Can it be reproduced based on the report?** If not, and the reporter won’t clarify, it’s likely not actionable.
> * **Is there a real security impact (CIA)?** Crashes or panics aren’t security issues unless they expose data or break trust boundaries.
> * **Is the exploit practical or purely theoretical?** Actionable reports usually describe a realistic threat scenario.
> * **Does it involve untrusted/malicious input?** If yes, it’s worth investigating. If not, it may just be a bug, not a vulnerability.
> * **Would the issue only occur through clearly unsafe or ill-advised use?** If a user must take actions that knowingly violate security best practices or common sense, the issue may be considered out-of-scope for immediate response.
> * **Does the issue assume the attacker already controls the system?** If exploitation requires prior full compromise (e.g. root access or administrator rights), it’s typically not considered a standalone vulnerability.
>
### 🧾 Record

> 📘 **Info:**  
> Enter your answers directly into the list below

- Is there a direct risk of [CIA](https://www.energy.gov/femp/operational-technology-cybersecurity-energy-systems#cia) being broken? `Yes|No`
- Which part of [CIA](https://www.energy.gov/femp/operational-technology-cybersecurity-energy-systems#cia) could be broken? `Confidentiality|Integrity|Availability`
- What user data is at risk?
- What is required to exploit this situation?
- How does the exploit work? _(Briefly summarize the exploit in your own words)_
- The vulnerability was introduced on: `YYYY-MM-DD`
- Does this violate the project’s threat model? `Yes|No|Unclear|n/a` (n/a = no threat model available)
- Is there a pull request where the vulnerability was introduced? 
- Is the vulnerable code still present in latest release? `Yes|No|Partially`

---

## Phase 2 - Mitigation

Now that the issue is confirmed and actionable, this phase is focused on mitigating risk. This may involve developing a patch, implementing temporary workarounds, or updating documentation to warn users.

If disclosure is necessary (e.g. CVE, GitHub Advisory publication), preparation begins here.

> [!TIP]
> In this phase, your job is to stop the bleeding. That means:
>  - Confirm the vulnerability is properly fixed or mitigated in all affected versions.
>  - Make sure tests cover the fix so it doesn't silently regress.
>  - Decide if your fix is just a quick patch or addresses the root cause—and document which it is.
>  - Don’t move on until you're confident the risk is contained and downstream users won't get burned.

### ✅ Tasks
_Things that must be addressed before ending mitigation phase_

- [ ] Check if the vulnerability is already fixed in latest versions.
	- [ ] If yes, determine if any mitigation is still required (e.g. backport, disclosure, workaround).
		- [ ] If no mitigation is required, move on to `Phase 3 - Scoping`
- [ ] Confirm which libraries, packages, and versions are affected.
- [ ] Prepare or finalize a code fix.
- [ ] Confirm mitigation effectiveness across all affected packages/configs.
	- [ ] Were tests added or updated to cover the vulnerability?
- [ ] Determine if the fix is tactical (“stop the bleeding”) or comprehensive (“addresses the root cause”).
	- [ ] If tactical, is it sufficient to mitigate the vulnerability?
- [ ] Have fixes been released for all supported or relevant versions?
	- [ ] If not, are release branches prepared and scheduled?
	- [ ] Are any patches still pending publication (e.g. Package-Managers)?

### 🧾 Record

> 📘 **Info:**  
> Enter your answers directly into the list below

- The vulnerability was first mitigated on: `YYYY-MM-DD`
- Is there a link to the mitigation work? 
- Was the fix tactical or comprehensive? `Tactical|Comprehensive`
- Were new tests added or updated to validate the fix? `Yes|No`
- Does the fix need to be backported? `Yes|No|In progress`
- Which package versions now include the fix?

---

## Phase 3 - Scoping

Now that the vulnerability is mitigated, this phase is focused on assessing the **real-world impact**. That includes:

* Determining who (if anyone) was affected
* Confirming whether there was actual exploitation
* Identifying ecosystem-wide blast radius
* Deciding if this qualifies as a formal *incident*

> [!TIP]
> Scoping asks:
> “Did anyone actually get owned?”
> “Did we ship something that made it into the wild?”
> “Do we need to notify or coordinate with other projects?”

### ✅ Tasks
_Things that should be completed before moving on_

- [ ] Check package download stats (e.g. NuGet) for affected versions.
- [ ] Confirm if the vulnerability could be exploited under normal usage.
- [ ] Search for signs of public exploitation (e.g. GitHub issues, tweets, blog posts).
- [ ] Decide whether the issue warrants public disclosure or a GitHub security advisory.

### 🧠 Notes

> [!TIP]  
> Add your reasoning and discoveries here. Include search queries, heuristics, test cases, or external feedback that helped validate impact.

### 🧾 Record

> 📘 **Info:**  
> Enter your answers directly into the list below

- Estimated reach: `X downloads`    
- Is the issue likely to be exploited in the wild? `Yes|No|Unclear`    
- Any known public exploitation? `Yes|No`    
- Will a GitHub advisory be published? `Yes|No|Pending`
---

## Phase 4 - Disclosure

> [!WARNING]
> **Do not publish the GitHub Security Advisory before the fixed packages are available.**
> Users must be able to upgrade immediately—never announce a vulnerability without a working patch on NuGet and/or GitHub Releases.

This phase focuses on releasing the fix and publishing a security advisory. Your job here is to notify affected users through GitHub mechanisms and relevant community channels, without oversharing sensitive details or drawing attention before packages are available.
### ✅ Tasks
_Things that should be completed before moving on_

- [ ] Are we going to notify for this case? `Yes|No`

If `No`, feel free to delete the checklist below 👇

If `Yes`, complete the following to ensure users are informed and have everything they need to update safely:

> [!TIP]
> A good advisory should help users **understand the risk** and **act quickly**. Include:
>
> * What the vulnerability is and why it matters (1–2 sentence summary)
> * Affected versions and fixed versions
> * Clear upgrade instructions
> * Any available workarounds
> * Acknowledgement (if applicable)
>
> Stay clear, specific, and don’t overshare exploit details unless necessary.

- [ ] Final release(s) with fixes are published to NuGet
- [ ] GitHub Security Advisory is published and includes all key details:
  - [ ] Summary and impact
  - [ ] Affected and fixed versions
  - [ ] Mitigation or upgrade instructions
- [ ] GitHub release notes link to the advisory
- [ ] (Optional) GitHub Discussion created to answer questions
- [ ] (Optional) Shared update on relevant channels (e.g. Slack)

### 🧾 Record

> 📘 **Info:**  
> Enter your answers directly into the list below

- When was the advisory published? `YYYY-MM-DD:HH-MM-SSZ`    
- Link to GitHub Security Advisory: `<URL>`    
- Link to mitigation PR(s): `<URL>`    
- When was the mitigation PR merged? `YYYY-MM-DD`    
- When were patched NuGet packages available? `YYYY-MM-DD`    
- Link to blog posts or announcements (if any): `<URL>`    
- Link to GitHub Discussion (if any): `<URL>`

## Phase 5 - Response Evaluation

This phase is for reviewing the effectiveness of your response. Were the right steps taken? Did the process hold up? What should be improved for next time?

> [!TIP]  
> Focus on evaluating how the response went—what worked, what didn’t, and what should change.


* [ ] Identify what worked well in the response process.
* [ ] Note what caused confusion, delay, or unnecessary work.
* [ ] Recommend improvements to the checklist, workflow, or communications.
* [ ] File issues or tasks for process or tooling updates.
* [ ] Update this document or templates to reflect changes.
### 🧾 Record

> 📘 **Info:**  
> Enter your answers directly into the list below

- What went well?    
- What could be improved?    
- Were any checklist/process updates made? `Yes|No`    
- Are there open follow-ups to track? List URLs: `<...>`