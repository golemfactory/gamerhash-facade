# app.py
import time
import json

from flask import Flask, Response, request, stream_with_context
from flask_accept import accept
import base64

app = Flask(__name__)

def read_image():
    with open("img.png", "rb") as image_file:
        encoded_string = base64.b64encode(image_file.read())
    return encoded_string


@app.post("/sdapi/v1/txt2img")
@accept("application/json")
def get_image():
    response = {
        "images": [],
        "prompt": ""
    }
    print('resonse: ', response)

    if request.is_json:
        r = request.get_json(force=True)
        response['prompt'] = r['prompt']
        response['images'].append(read_image().decode("utf-8"))
        return response, 200
    return {"error": "Request must be JSON"}, 415


@get_image.support("application/octet-stream")
def stream_image():
    def img_stream(prompt):
        
        response = {
            "images": [],
            "prompt": ""
        }
        print('response: ', response)
        
        response['prompt'] = prompt
        response['images'].append(read_image().decode("utf-8"))
        response = json.dumps(response)
       
        start = 0
        for end in range(0, len(response), 65536):
            yield response[start:end]
            start = end
        if start < len(response):
            yield response[start:]

    if request.is_json:
        r = request.get_json(force=True)
        return Response(stream_with_context(img_stream(r['prompt'])), mimetype="application/octet-stream")
    return {"error": "Request must be JSON"}, 415




