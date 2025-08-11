#!/bin/bash

# Test script for the .NET payment processing system

API_URL="http://localhost:9999"

echo "🚀 Testing .NET Payment Processing System"
echo "=========================================="

# Test 1: Health check (payments endpoint)
echo ""
echo "📍 Test 1: Health Check"
echo "----------------------"
response=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/payments-summary")
if [ "$response" = "200" ]; then
    echo "✅ API is responding (HTTP $response)"
else
    echo "❌ API not responding (HTTP $response)"
    exit 1
fi

# Test 2: Submit payment
echo ""
echo "📍 Test 2: Submit Payment"
echo "-------------------------"
payment_data='{"correlationId":"test-payment-001","amount":125.50}'
response=$(curl -s -X POST "$API_URL/payments" \
    -H "Content-Type: application/json" \
    -d "$payment_data" \
    -w "%{http_code}")

if [[ "$response" == *"200" ]]; then
    echo "✅ Payment submitted successfully"
else
    echo "❌ Payment submission failed (HTTP $response)"
fi

# Test 3: Submit multiple payments
echo ""
echo "📍 Test 3: Submit Multiple Payments"
echo "-----------------------------------"
for i in {1..10}; do
    correlation_id="test-payment-$(printf "%03d" $i)"
    amount=$(awk "BEGIN {printf \"%.2f\", $i * 10.25}")
    payment='{"correlationId":"'$correlation_id'","amount":'$amount'}'
    
    curl -s -X POST "$API_URL/payments" \
        -H "Content-Type: application/json" \
        -d "$payment" > /dev/null
    
    echo "📋 Submitted payment $i: $correlation_id ($amount)"
done

# Wait for processing
echo ""
echo "⏳ Waiting for payments to process..."
sleep 2

# Test 4: Check payment summary
echo ""
echo "📍 Test 4: Payment Summary"
echo "--------------------------"
summary=$(curl -s "$API_URL/payments-summary")
echo "📊 Payment Summary:"
echo "$summary" | jq . 2>/dev/null || echo "$summary"

# Test 5: Admin purge
echo ""
echo "📍 Test 5: Admin Purge"
echo "----------------------"
purge_response=$(curl -s -X DELETE "$API_URL/admin/purge")
echo "🗑️ Purge Response:"
echo "$purge_response" | jq . 2>/dev/null || echo "$purge_response"

# Test 6: Verify purge
echo ""
echo "📍 Test 6: Verify Purge"
echo "-----------------------"
summary_after_purge=$(curl -s "$API_URL/payments-summary")
echo "📊 Summary After Purge:"
echo "$summary_after_purge" | jq . 2>/dev/null || echo "$summary_after_purge"

echo ""
echo "🎉 Test suite completed!"
echo "========================="
