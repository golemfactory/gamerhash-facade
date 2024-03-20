# app.py
from flask import Flask, request, jsonify
import base64

app = Flask(__name__)

response = {
    "images": [],
    "prompt": ""
}

def read_image():
    with open("img.png", "rb") as image_file:
        encoded_string = base64.b64encode(image_file.read())
    return encoded_string




@app.post("/sdapi/v1/txt2img")
def add_country():
    if request.is_json:
        r = request.get_json(force=True)
        print(r)
        print(r['prompt'])
        response['prompt'] = r['prompt']
        response['images'].append(read_image().decode("utf-8"))
        return response, 200
    return {"error": "Request must be JSON"}, 415



