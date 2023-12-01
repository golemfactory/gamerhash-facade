
fail() {
	printf "%s\n" "$1" >&2
	exit 1
}

not_empty() {
	test -z "$1" && fail "expected $2"
}


not_empty "$GITHUB_REF" GITHUB_REF
not_empty "$OS_NAME" OS_NAME


if [ "$OS_NAME" = "ubuntu" ]; then
  OS_NAME=linux
  exe=""
elif [ "$OS_NAME" == "linux-aarch64" ]; then
  OS_NAME=linux_aarch64
  exe=""
elif [ "$OS_NAME" == "macos" ]; then
  OS_NAME=osx
elif [ "$OS_NAME" == "windows" ]; then
  exe=".exe"
else
  fail "unknown os name: $OS_NAME"
fi

TAG_NAME="${GITHUB_REF##*/}"

generate_asset() {
  local asset_type=$1
  local TARGET_DIR="releases/"${asset_type}-${OS_NAME}-${TAG_NAME}

  mkdir -p "$TARGET_DIR"
  mv package/* "$TARGET_DIR"

  if [ "$OS_NAME" = "windows" ]; then
    echo "::set-output name=${asset_type}Artifact::${asset_type}-${OS_NAME}-${TAG_NAME}.zip"
    echo "::set-output name=${asset_type}Media::application/zip"
    (cd "$TARGET_DIR" && 7z a "../${asset_type}-${OS_NAME}-${TAG_NAME}.zip" * )
  else
    echo "::set-output name=${asset_type}Artifact::${asset_type}-${OS_NAME}-${TAG_NAME}.tar.gz"
    echo "::set-output name=${asset_type}Media::application/tar+gzip"
    (cd releases && tar czf "${asset_type}-${OS_NAME}-${TAG_NAME}.tar.gz" "${asset_type}-${OS_NAME}-${TAG_NAME}")
    du -h "releases/${asset_type}-${OS_NAME}-${TAG_NAME}.tar.gz"
  fi
}

generate_asset "golem-ai"
