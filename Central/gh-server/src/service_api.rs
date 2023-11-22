use serde::{Deserialize, Serialize};

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RunTaskBody {
    pub model: String,
    pub runtime: String,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StatusResponse {
    // 2 layers of status:
    // - What server knows (for example: client disconnected)
    // - What client reported (for example: image deployed)
}
