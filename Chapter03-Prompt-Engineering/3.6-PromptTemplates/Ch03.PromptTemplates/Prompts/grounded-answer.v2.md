---
name: grounded-answer
version: 2
description: Answers a customer question using supplied policy excerpts, with citations.
temperature: 0.2
---
Answer the customer's question using only the policy excerpts below.
If the excerpts do not contain the answer, say that you are not sure and offer to
connect the customer with a human agent.
Cite the document id in square brackets after each fact you use.

<policy_excerpts>
{{excerpts}}
</policy_excerpts>

<customer_question>
{{question}}
</customer_question>
