# Simple http server that listens on port 7861 and endpoint /sdapi/v1/txt2img

## Setup

```ps1
python -m pip install virtualenv
python -m virtualenv .venv
.\.venv\Scripts\activate
# or linux equivalent
pip install poetry
poetry shell
```

## Start

`flask run --port 7861`

expects a json request with
{
  "prompt": ""
}

returns json:
{
  "images": [],
  "prompt": ""
}

where images contains base64 encoded img.png and prompt is compied from request
