Example of a python script that uses ai_runtime to generate an image based on a prompt.


```
poetry install
poetry run python ai_runtime.py --network goerli --driver erc20next
```

Requires a running requestor yagna. An example `.env` can be used before running the python script:

```
yagna service run
export $(grep -v '^#' .env | xargs)
```
