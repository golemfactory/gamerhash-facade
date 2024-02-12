Example of a python script that uses ai_runtime to generate an image based on a prompt.


```
poetry install
poetry run python ai_runtime.py --network goerli --driver erc20next
```

Requires a running requestor yagna with support for http proxying over GSB. An example of loadng `.env` that can be used before running the python script:

```
yagna service run > yagn.log &
export $(grep -v '^#' .env | xargs)
yagna payment fund --network goerli --driver erc20next
```
