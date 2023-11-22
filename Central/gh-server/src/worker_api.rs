use semver::Version;
use serde::{Deserialize, Serialize};

use ya_client::model::NodeId;

#[derive(Deserialize, Serialize)]
pub struct Price {
    #[serde(rename = "golem.usage.duration-sec")]
    duration: f64,
    #[serde(rename = "golem.usage.gpu-sec")]
    gpu_usage: f64,
    #[serde(rename = "ai-runtime.requests")]
    num_requests: f64,
    #[serde(rename = "init-price")]
    init_price: f64,
}

#[derive(Deserialize, Serialize)]
pub struct Runtime {
    name: String,
    version: Version,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RegisterBody {
    wallet: NodeId,
    price: Price,
    runtimes: Vec<Runtime>,
    // TODO: GPU info
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RegisterResponse {}

#[cfg(test)]
mod tests {
    use super::*;
    use std::str::FromStr;

    #[test]
    fn print_rest_api() {
        let body = RegisterBody {
            id: NodeId::from_str("0x95369fc6fd02afeca110b9c32a21fb8ad899ee0a").unwrap(),
            wallet: NodeId::from_str("0xa5ad3f81e283983b8e9705b2e31d0c138bb2b1b7").unwrap(),
            price: Price {
                duration: 0.001,
                gpu_usage: 0.001,
                num_requests: 0.01,
                init_price: 0.0,
            },
            runtimes: vec![Runtime {
                name: "Automatic".to_string(),
                version: Version::new(1, 6, 0),
            }],
        };
        let body = serde_json::to_string_pretty(&body).unwrap();
        println!("Body: {body}")
    }
}
