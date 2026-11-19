#!/bin/sh
set -eu

create_topic() {
    topic_name="$1"

    topic_arn="$(aws sns create-topic --name "$topic_name" --query TopicArn --output text)"
    created_topic_arn="$(
        aws sns list-topics \
            --query "Topics[?TopicArn=='$topic_arn'].TopicArn | [0]" \
            --output text
    )"

    test "$created_topic_arn" = "$topic_arn"
    printf "%s" "$topic_arn"
}

create_queue() {
    queue_name="$1"
    topic_arn="$2"

    queue_url="$(aws sqs create-queue --queue-name "$queue_name" --query QueueUrl --output text)"
    queue_arn="$(
        aws sqs get-queue-attributes \
            --queue-url "$queue_url" \
            --attribute-names QueueArn \
            --query "Attributes.QueueArn" \
            --output text
    )"

    cat > /tmp/queue-attributes.json <<EOF
{
  "Policy": "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"sqs:SendMessage\",\"Resource\":\"$queue_arn\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"$topic_arn\"}}}]}"
}
EOF

    aws sqs set-queue-attributes \
        --queue-url "$queue_url" \
        --attributes file:///tmp/queue-attributes.json

    created_queue_url="$(aws sqs get-queue-url --queue-name "$queue_name" --query QueueUrl --output text)"
    queue_policy="$(
        aws sqs get-queue-attributes \
            --queue-url "$created_queue_url" \
            --attribute-names Policy \
            --query "Attributes.Policy" \
            --output text
    )"

    test "$created_queue_url" = "$queue_url"
    printf "%s" "$queue_policy" | grep -F "$topic_arn" > /dev/null
    printf "%s" "$queue_policy" | grep -F "$queue_arn" > /dev/null
    printf "%s|%s" "$queue_url" "$queue_arn"
}

create_subscription() {
    topic_arn="$1"
    queue_arn="$2"

    subscription_arn="$(
        aws sns subscribe \
            --topic-arn "$topic_arn" \
            --protocol sqs \
            --notification-endpoint "$queue_arn" \
            --attributes RawMessageDelivery=true \
            --query SubscriptionArn \
            --output text
    )"
    created_subscription_arn="$(
        aws sns list-subscriptions-by-topic \
            --topic-arn "$topic_arn" \
            --query "Subscriptions[?Endpoint=='$queue_arn' && Protocol=='sqs'].SubscriptionArn | [0]" \
            --output text
    )"

    test "$created_subscription_arn" = "$subscription_arn"
    test "$created_subscription_arn" != "None"

    subscription_endpoint="$(
        aws sns get-subscription-attributes \
            --subscription-arn "$created_subscription_arn" \
            --query "Attributes.Endpoint" \
            --output text
    )"
    subscription_raw_delivery="$(
        aws sns get-subscription-attributes \
            --subscription-arn "$created_subscription_arn" \
            --query "Attributes.RawMessageDelivery" \
            --output text
    )"

    test "$subscription_endpoint" = "$queue_arn"
    test "$subscription_raw_delivery" = "true"
    printf "%s" "$created_subscription_arn"
}

topic_name="waste_obligations_analytics_events"
queue_name="waste_obligations_analytics_events_queue"

topic_arn="$(create_topic "$topic_name")"
queue="$(create_queue "$queue_name" "$topic_arn")"
queue_arn="${queue#*|}"
create_subscription "$topic_arn" "$queue_arn" > /dev/null

echo "Ready: verified $queue_name is subscribed to $topic_name"
