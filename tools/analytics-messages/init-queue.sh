#!/bin/sh
set -eu

topic_name="analytics_message_samples"
queue_name="analytics_message_samples_queue"
attributes_file="$(mktemp)"
trap 'rm -f "$attributes_file"' 0
trap 'exit 1' HUP INT TERM

topic_arn="$(aws sns create-topic --name "$topic_name" --query TopicArn --output text)"
queue_url="$(aws sqs create-queue --queue-name "$queue_name" --query QueueUrl --output text)"
queue_arn="$(
    aws sqs get-queue-attributes \
        --queue-url "$queue_url" \
        --attribute-names QueueArn \
        --query Attributes.QueueArn \
        --output text
)"

cat > "$attributes_file" <<EOF
{
  "Policy": "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"sqs:SendMessage\",\"Resource\":\"$queue_arn\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"$topic_arn\"}}}]}"
}
EOF

aws sqs set-queue-attributes \
    --queue-url "$queue_url" \
    --attributes "file://$attributes_file"

subscription_arn="$(
    aws sns subscribe \
        --topic-arn "$topic_arn" \
        --protocol sqs \
        --notification-endpoint "$queue_arn" \
        --attributes RawMessageDelivery=true \
        --query SubscriptionArn \
        --output text
)"

test "$subscription_arn" != "None"
test "$(
    aws sns get-subscription-attributes \
        --subscription-arn "$subscription_arn" \
        --query Attributes.Endpoint \
        --output text
)" = "$queue_arn"
test "$(
    aws sns get-subscription-attributes \
        --subscription-arn "$subscription_arn" \
        --query Attributes.RawMessageDelivery \
        --output text
)" = "true"

printf 'Ready: %s is subscribed to %s with raw message delivery.\n' "$queue_name" "$topic_name"
