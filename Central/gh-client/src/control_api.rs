use serde::{Deserialize, Serialize};
use ya_client::model::NodeId;

// WebSocket: Server -> Client

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RunTask {
    requestor_id: NodeId,
    // TODO ...
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FinishTask {
    // TODO ...
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProxyRequest {
    // TODO ...
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PaymentStatus {
    // TODO ...
}

// WebSocket: Client -> Server

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ReportUsage {
    // TODO ...
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ReportState {
    // TODO ...
}
