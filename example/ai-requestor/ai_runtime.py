import asyncio
import base64
import io
import json
import os
from PIL import Image
import requests

from dataclasses import dataclass
from datetime import datetime

from yapapi import Golem
from yapapi.payload import Payload
from yapapi.props import inf
from yapapi.props.base import constraint, prop
from yapapi.services import Service
from yapapi.log import enable_default_logger
from yapapi.config import ApiConfig

import argparse
import asyncio
import tempfile
from datetime import datetime, timezone
from pathlib import Path

import colorama  # type: ignore

from yapapi import Golem, NoPaymentAccountError
from yapapi import __version__ as yapapi_version
from yapapi import windows_event_loop_fix
from yapapi.log import enable_default_logger
from yapapi.strategy import SCORE_TRUSTED, SCORE_REJECTED, MarketStrategy
from yapapi.rest import Activity

from ya_activity import ApiClient, ApiException, RequestorControlApi, RequestorStateApi

# Utils

TEXT_COLOR_RED = "\033[31;1m"
TEXT_COLOR_GREEN = "\033[32;1m"
TEXT_COLOR_YELLOW = "\033[33;1m"
TEXT_COLOR_BLUE = "\033[34;1m"
TEXT_COLOR_MAGENTA = "\033[35;1m"
TEXT_COLOR_CYAN = "\033[36;1m"
TEXT_COLOR_WHITE = "\033[37;1m"

TEXT_COLOR_DEFAULT = "\033[0m"

colorama.init()


def build_parser(description: str) -> argparse.ArgumentParser:
    current_time_str = datetime.now(tz=timezone.utc).strftime("%Y%m%d_%H%M%S%z")
    default_log_path = Path(tempfile.gettempdir()) / f"yapapi_{current_time_str}.log"

    parser = argparse.ArgumentParser(description=description)
    parser.add_argument(
        "--payment-driver", "--driver", help="Payment driver name, for example `erc20`"
    )
    parser.add_argument(
        "--payment-network", "--network", help="Payment network name, for example `holesky`"
    )
    parser.add_argument("--subnet-tag", help="Subnet name, for example `public`")
    parser.add_argument(
        "--log-file",
        default=str(default_log_path),
        help="Log file for YAPAPI; default: %(default)s",
    )
    return parser


def format_usage(usage):
    return {
        "current_usage": usage.current_usage,
        "timestamp": usage.timestamp.isoformat(sep=" ") if usage.timestamp else None,
    }


def print_env_info(golem: Golem):
    print(
        f"yapapi version: {TEXT_COLOR_YELLOW}{yapapi_version}{TEXT_COLOR_DEFAULT}\n"
        f"Using subnet: {TEXT_COLOR_YELLOW}{golem.subnet_tag}{TEXT_COLOR_DEFAULT}, "
        f"payment driver: {TEXT_COLOR_YELLOW}{golem.payment_driver}{TEXT_COLOR_DEFAULT}, "
        f"and network: {TEXT_COLOR_YELLOW}{golem.payment_network}{TEXT_COLOR_DEFAULT}\n"
    )


def run_golem_example(example_main, log_file=None):
    # This is only required when running on Windows with Python prior to 3.8:
    windows_event_loop_fix()

    if log_file:
        enable_default_logger(
            log_file=log_file,
            debug_activity_api=True,
            debug_market_api=True,
            debug_payment_api=True,
            debug_net_api=True,
        )

    loop = asyncio.get_event_loop()
    task = loop.create_task(example_main)

    try:
        loop.run_until_complete(task)
    except NoPaymentAccountError as e:
        handbook_url = (
            "https://handbook.golem.network/requestor-tutorials/"
            "flash-tutorial-of-requestor-development"
        )
        print(
            f"{TEXT_COLOR_RED}"
            f"No payment account initialized for driver `{e.required_driver}` "
            f"and network `{e.required_network}`.\n\n"
            f"See {handbook_url} on how to initialize payment accounts for a requestor node."
            f"{TEXT_COLOR_DEFAULT}"
        )
    except KeyboardInterrupt:
        print(
            f"{TEXT_COLOR_YELLOW}"
            "Shutting down gracefully, please wait a short while "
            "or press Ctrl+C to exit immediately..."
            f"{TEXT_COLOR_DEFAULT}"
        )
        task.cancel()
        try:
            loop.run_until_complete(task)
            print(
                f"{TEXT_COLOR_YELLOW}Shutdown completed, thank you for waiting!{TEXT_COLOR_DEFAULT}"
            )
        except (asyncio.CancelledError, KeyboardInterrupt):
            pass


class ProviderOnceStrategy(MarketStrategy):
    """Hires provider only once.
    """

    def __init__(self):
        self.history = set(())

    async def score_offer(self, offer):
        if offer.issuer not in self.history:
            return SCORE_TRUSTED
        else:
            return SCORE_REJECTED


    def remember(self, provider_id: str):
        self.history.add(provider_id)

# App

RUNTIME_NAME = "automatic" 
#RUNTIME_NAME = "dummy"

@dataclass
class AiPayload(Payload):
    image_url: str = prop("golem.srv.comp.ai.model")
    image_fmt: str = prop("golem.srv.comp.ai.model-format", default="safetensors")

    runtime: str = constraint(inf.INF_RUNTIME_NAME, default=RUNTIME_NAME)


class AiRuntimeService(Service):
    @staticmethod
    async def get_payload():
        ## TODO switched into using smaller model to avoid problems during tests. Resolve it when automatic runtime integrated
        # return AiPayload(image_url="hash:sha3:92180a67d096be309c5e6a7146d89aac4ef900e2bf48a52ea569df7d:https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors?download=true")
        # return AiPayload(image_url="hash:sha3:0b682cf78786b04dc108ff0b254db1511ef820105129ad021d2e123a7b975e7c:https://huggingface.co/cointegrated/rubert-tiny2/resolve/main/model.safetensors?download=true")
        return AiPayload(image_url="hash:sha3:b2da48d618beddab1887739d75b50a3041c810bc73805a416761185998359b24:https://huggingface.co/runwayml/stable-diffusion-v1-5/resolve/main/v1-5-pruned-emaonly.safetensors?download=true")
    async def start(self):
        self.strategy.remember(self._ctx.provider_id)

        script = self._ctx.new_script(timeout=None)
        script.deploy()
        script.start()
        yield script

    # async def run(self):
    #    # TODO run AI tasks here

    def __init__(self, strategy: ProviderOnceStrategy):
        super().__init__()
        self.strategy = strategy


async def trigger(activity: RequestorControlApi, token, prompt, output_file):

    custom_url = "/sdapi/v1/txt2img"
    url = activity._api.api_client.configuration.host + f"/activity/{activity.id}/proxy-http" + custom_url

    stream = True

    if stream:
        headers = {"Authorization": "Bearer "+token, "Accept": "application/octet-stream-2"}
    else:
        headers = {"Authorization": "Bearer "+token, "Accept": "application/json"}

    payload = {
        'prompt': prompt,
        'steps': 250
    }

    print('Sending request:')
    payload_str = str(payload).replace("'", "\\\"")
    if stream:
        print(f'curl -X POST -H \'Authorization: Bearer {token}\' -H "Content-Type: application/json; charset=utf-8"  -H "Accept: application/octet-stream" -d "{payload_str}" {url}')
    else:
        print(f'curl -X POST -H \'Authorization: Bearer {token}\' -H "Content-Type: application/json; charset=utf-8"  -H "Accept: application/json" -d "{payload_str}" {url}')
    
    response = requests.post(url, headers=headers, json=payload, stream=stream)
    # if response.status_code != 200:
    print(f'Reponse status code: {response.status_code}');
    print(response.headers)
    if response.encoding is None:
        response.encoding = 'utf-8'

    if stream:
        if response.ok:
            response = json.loads(response.text)
            image = Image.open(io.BytesIO(base64.b64decode(response['images'][0])))
            print(f"Saving response to {os.path.abspath(output_file)}")
            image.save(output_file)
        else:
            print(f"Error code: {response.status_code}, message: {response.text}")
    else:
        content = ""
        with open("content.txt", "w") as text_file:
            for line in response.iter_lines(decode_unicode=True):
                if line:
                    text_file.write(line)
                    content += line

        response = json.loads(content)
        image = Image.open(io.BytesIO(base64.b64decode(response['images'][0])))
        print(f"Saving response to {os.path.abspath(output_file)}")
        image.save(output_file)


async def main(subnet_tag, driver=None, network=None):
    strategy = ProviderOnceStrategy()
    async with Golem(
        budget=10.0,
        subnet_tag=subnet_tag,
        strategy=strategy,
        payment_driver=driver,
        payment_network=network,
    ) as golem:
        cluster = await golem.run_service(
            AiRuntimeService,
            instance_params=[
                {"strategy": strategy}
            ],
            num_instances=1,
        )

        def instances():
            return [
                {
                    'name': s.provider_name,
                    'state': s.state.value,
                    'context': s._ctx
                } for s in cluster.instances
            ]

        async def get_image(prompt, file_name):
            
            for s in cluster.instances:

                for _ in range(0, 10):
                    if s._ctx == None:
                        print(f'Context is {s._ctx} for: {s.provider_name} {s.state.value}... waiting')
                        await asyncio.sleep(1)
                    else:
                        break

                if s._ctx != None:
                    for id in [s._ctx._activity.id ]:
                        activity = await golem._engine._activity_api.use_activity(id)
                        await trigger(
                            activity,
                            golem._engine._api_config.app_key,
                            prompt,
                            file_name
                        )
                else:
                    print(f'...gave up')

        # Begin
        while True:
            i = instances()

            running = [r for r in i if r['state'] == 'running']
            
            print(f"""instances: {[f"{r['name']}: {r['state']}" for r in i]}""")

            if len(running) > 0:             
                print('Please type your prompt:')
                prompt = input()
                print('Sending to automatic')
                await get_image(
                    prompt,
                    'output.png'
                )
            
            await asyncio.sleep(3)
        # End 
        
if __name__ == "__main__":
    parser = build_parser("Run AI runtime task")
    now = datetime.now().strftime("%Y-%m-%d_%H.%M.%S")
    parser.set_defaults(log_file=f"ai-yapapi-{now}.log")
    args = parser.parse_args()

    run_golem_example(
        main(
            subnet_tag=args.subnet_tag,
            driver=args.payment_driver,
            network=args.payment_network,
        ),
        log_file=args.log_file,
    )
