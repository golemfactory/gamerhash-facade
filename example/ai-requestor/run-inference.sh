#!/bin/bash

set -e

BASE_URL="https://f48cc2fe53a0.app.modelserve.dev-test.golem.network"
SEQ_NUM=1
#PROMPT="Beatifull girl riding an unicorn flying above a rainbow."
PROMPT="close up portrait, Amidst the interplay of light and shadows in a photography studio,a soft spotlight traces the contours of a face,highlighting a figure clad in a sleek black turtleneck. The garment,hugging the skin with subtle luxury,complements the Caucasian model's understated makeup,embodying minimalist elegance. Behind,a pale gray backdrop extends,its fine texture shimmering subtly in the dim light,artfully balancing the composition and focusing attention on the subject. In a palette of black,gray,and skin tones,simplicity intertwines with profundity,as every detail whispers untold stories."

mkdir -p outputs

curl -X POST -H "Content-Type: application/json" \
    -d '{"prompt": "Beatifull girl riding an unicorn flying above a rainbow.", "num_inference_steps": 24, "guidance_scale": 3.5}' \
    ${BASE_URL}/sdapi/v1/txt2img \
    | jq -r ".images[0]" \
    | base64 --decode > outputs/output-${SEQ_NUM}.png \
    && xdg-open outputs/output-${SEQ_NUM}.png

