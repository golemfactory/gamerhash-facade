import asyncio
import os
import json

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone

from yapapi import Golem
from yapapi.payload import Payload
from yapapi.props import inf
from yapapi.props.base import constraint, prop
from yapapi.services import Service
from yapapi.log import enable_default_logger

import argparse
import asyncio
import tempfile
from pathlib import Path
from typing import Optional

import colorama  # type: ignore

from yapapi import Golem, NoPaymentAccountError
from yapapi import __version__ as yapapi_version
from yapapi import windows_event_loop_fix
from yapapi.log import enable_default_logger
from yapapi.strategy import SCORE_TRUSTED, SCORE_REJECTED, MarketStrategy
from yapapi.strategy.base import PropValueRange, PROP_DEBIT_NOTE_INTERVAL_SEC, PROP_PAYMENT_TIMEOUT_SEC

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
    parser.add_argument("--runtime", default="dummy", help="Runtime name, for example `automatic`")
    parser.add_argument("--descriptor", default=None, help="Path to node descriptor file")
    parser.add_argument("--pay-interval", default=180, help="Interval of making partial payments")
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

    def __init__(self, pay_interval=180):
        self.history = set(())
        self.acceptable_prop_value_range_overrides =  {
            PROP_DEBIT_NOTE_INTERVAL_SEC: PropValueRange(60, None),
            PROP_PAYMENT_TIMEOUT_SEC: PropValueRange(int(pay_interval), None),
        }

    async def score_offer(self, offer):
        if offer.issuer not in self.history:
            return SCORE_TRUSTED
        else:
            return SCORE_REJECTED


    def remember(self, provider_id: str):
        self.history.add(provider_id)

# App

@dataclass
class AiPayload(Payload):
    image_url: str = prop("golem.srv.comp.ai.model")
    image_fmt: str = prop("golem.srv.comp.ai.model-format", default="safetensors")
    
    node_descriptor: Optional[dict] = prop("golem.!exp.gap-31.v0.node.descriptor", default=None)

    runtime: str = constraint(inf.INF_RUNTIME_NAME, default="dummy")


class AiRuntimeService(Service):
    runtime: str
    node_descriptor: Optional[str] = None

    @staticmethod
    async def get_payload():
        if AiRuntimeService.node_descriptor:
            node_descriptor = json.loads(open(AiRuntimeService.node_descriptor, "r").read())
        else:
            node_descriptor = None
        
        if AiRuntimeService.runtime == "dummy":
            return AiPayload(
                image_url="hash:sha3:eb222a9f6afa502a379b2315ec9f1e853ba7013f7240bfa47fb2f455375fea9c:https://huggingface.co/timm/tf_mobilenetv3_small_minimal_100.in1k/resolve/main/model.safetensors?download=true",
                runtime="dummy",
                node_descriptor=node_descriptor
            )
        return AiPayload(
            image_url="hash:sha3:b2da48d618beddab1887739d75b50a3041c810bc73805a416761185998359b24:https://huggingface.co/runwayml/stable-diffusion-v1-5/resolve/main/v1-5-pruned-emaonly.safetensors?download=true",
            runtime="automatic",
            node_descriptor=node_descriptor
        )

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
        

async def main(subnet_tag, descriptor, driver=None, network=None, runtime="dummy", args=None):
    strategy = ProviderOnceStrategy(pay_interval=args.pay_interval)
    async with Golem(
        budget=4.0,
        subnet_tag=subnet_tag,
        strategy=strategy,
        payment_driver=driver,
        payment_network=network,
    ) as golem:
        AiRuntimeService.runtime = runtime
        AiRuntimeService.node_descriptor = descriptor
        cluster = await golem.run_service(
            AiRuntimeService,
            instance_params=[
                {"strategy": strategy}
            ],
            num_instances=1,
            expiration=datetime.now(timezone.utc) + timedelta(days=10),
        )

        async def print_usage():
            token = golem._engine._api_config.app_key

            activities = [
                s._ctx._activity.id
                for s in cluster.instances if s._ctx != None
            ]

            print(activities)
            
            for id in activities:
                activity = await golem._engine._activity_api.use_activity(id)
                custom_url = "/sdapi/v1/txt2img"
                url = activity._api.api_client.configuration.host + f"/activity/{activity.id}/proxy-http" + custom_url

                print('Request example:\n')
                if os.name == 'nt':
                    payload = '"prompt"="happy golem"'
                    headers = (
                        f"\"Authorization\" = \"Bearer {token}\"; "
                        "\"Content-Type\" = \"application/json; charset=utf-8\"; "
                        "\"Accept\" = \"text/event-stream\""
                    )
                    powershell_cmd = (
                        f"$images = Invoke-WebRequest -Method POST -Headers @{{ {headers} }} -Body (@{{ {payload} }}|ConvertTo-Json) -Uri {url} | ConvertFrom-Json | Select images | Select-Object -Index 0\n"
                        "$bytes = [Convert]::FromBase64String($images.images)\n"
                        "$filename = \"C:\\Windows\\Temp\\output.png\"\n"
                        "[IO.File]::WriteAllBytes($filename, $bytes)\n"
                        "explorer C:\\Windows\\Temp\\output.png\n"
                    )
                    print(powershell_cmd)
                else:
                    payload = '{ \\"prompt\\": \\"happy golem\\" }'
                    headers = (
                        f"-H \'Authorization: Bearer {token}\' "
                        "-H \'Content-Type: application/json; charset=utf-8\' "
                        "-H \'Accept: text/event-stream\' "
                    )
                    pipe_image_cmd = '| jq -r ".images[0]" | base64 --decode > output.png && xdg-open output.png'
                    print(f'curl -X POST {headers} -d "{payload}" {url} {pipe_image_cmd}')

        def instances():
            return [
                {
                    'name': s.provider_name,
                    'state': s.state.value,
                    'context': s._ctx
                } for s in cluster.instances
            ]

        usage_printed = False
        while True:
            await asyncio.sleep(3)

            i = instances()

            running = [r for r in i if not r['context'] == None]
            if not usage_printed and len(running) > 0:
                await print_usage()
                usage_printed = True
            
            print(f"""instances: {[f"{r['name']}: {r['state']}" for r in i]}""")


if __name__ == "__main__":
    parser = build_parser("Run AI runtime task")
    now = datetime.now().strftime("%Y-%m-%d_%H.%M.%S")
    parser.set_defaults(log_file=f"ai-yapapi-{now}.log")
    args = parser.parse_args()

    run_golem_example(
        main(
            subnet_tag=args.subnet_tag,
            descriptor=args.descriptor,
            driver=args.payment_driver,
            network=args.payment_network,
            runtime=args.runtime,
            args=args
        ),
        log_file=args.log_file,
    )
