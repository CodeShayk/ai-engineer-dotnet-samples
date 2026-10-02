---
name: grounded-answer
version: 1
description: Answers a customer question using supplied policy excerpts.
temperature: 0.2
---
Answer the customer's question using the policy excerpts below.

<policy_excerpts>
{{excerpts}}
</policy_excerpts>

<customer_question>
{{question}}
</customer_question>
