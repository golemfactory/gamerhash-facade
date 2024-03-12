Simple http server that listens on port 7861 and endpoint /sdapi/v1/txt2img

expects a json request with
{
  "prompt": ""
}

returns json:
{
  "images": [],
  "prompt": ""
}

where images contains base53 encoded img.png and prompt is compied from request
