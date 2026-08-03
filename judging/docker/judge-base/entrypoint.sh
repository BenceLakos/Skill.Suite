#!/usr/bin/env bash
# Default white-box judge pipeline: graft the competitor's folder, restore offline, build, run.
#
# A session image overrides this only when it needs a different pipeline (see the black-box template).
# Everything session-specific comes from environment variables, so this file is never edited per session.

set -euo pipefail

. /usr/local/lib/judge-lib.sh

judge_configure
judge_install_traps

judge_require JUDGE_SWAP_SRC JUDGE_SOLUTION JUDGE_TEST_PROJECT

swap_dir
restore_offline
build_tests
run_tests
judge_assert_results
