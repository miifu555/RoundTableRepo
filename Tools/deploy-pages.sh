#!/usr/bin/env bash
# Build/WebGL の中身を gh-pages ブランチとして push する。
#
#   bash Tools/deploy-pages.sh
#
# gh-pages は毎回「1コミットだけの orphan ブランチ」として作り直して force push する。
# WebGL の出力は 28MB ほどの差分が効かないバイナリなので、履歴を積むとリポジトリが太るため。
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="$REPO_ROOT/Build/WebGL"
BRANCH="gh-pages"
# ローカルに同名ブランチが残っていると orphan を作れないので、毎回使い捨ての名前で作る
TMP_BRANCH="pages-deploy-$$"
WORKTREE="$(mktemp -d)/pages"

if [ ! -f "$BUILD_DIR/index.html" ]; then
  echo "エラー: $BUILD_DIR/index.html がありません。" >&2
  echo "先に Unity メニュー「Round Table / WebGL をビルド」を実行してください。" >&2
  exit 1
fi

cd "$REPO_ROOT"

cleanup() {
  local status=$?
  git worktree remove --force "$WORKTREE" 2>/dev/null || true
  git worktree prune 2>/dev/null || true
  git branch -D "$TMP_BRANCH" >/dev/null 2>&1 || true
  if [ "$status" -ne 0 ]; then
    echo "!! デプロイに失敗しました (exit $status)" >&2
  fi
}
trap cleanup EXIT

echo "==> 作業用ワークツリーを作る: $WORKTREE"
git worktree add --detach "$WORKTREE" >/dev/null

cd "$WORKTREE"
git switch --orphan "$TMP_BRANCH" >/dev/null

echo "==> ビルド成果物をコピー"
cp -r "$BUILD_DIR"/. .

# GitHub Pages の Jekyll 処理を止める (アンダースコア始まりのファイルが消えるのを防ぐ)
touch .nojekyll

git add -A
git -c user.name="$(git -C "$REPO_ROOT" config user.name)" \
    -c user.email="$(git -C "$REPO_ROOT" config user.email)" \
    commit -q -m "Publish WebGL build $(date '+%Y-%m-%d %H:%M')"

echo "==> push (force)"
git push -f origin "HEAD:refs/heads/$BRANCH"

echo
echo "完了。GitHub の Settings → Pages で"
echo "  Source: Deploy from a branch / Branch: $BRANCH / フォルダ: / (root)"
echo "を選ぶと公開されます。"
