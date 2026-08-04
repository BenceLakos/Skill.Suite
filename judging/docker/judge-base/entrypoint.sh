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

# Taken FIRST, while the hidden suite is the only code in the container. After swap_dir the competitor's folder
# is present, and anything recorded then could in principle have been influenced by it.
judge_capture_test_manifest

# In a white-box session the graded asset is the hidden suite itself. The test step already runs unprivileged and
# cannot write it, but build_tests runs as root after the swap, so sealing is a cheap second line: if the suite
# that produced the results is not the suite the session shipped, the results mean nothing.
export JUDGE_SERVICES_SRC="${JUDGE_SERVICES_SRC:-$(dirname "${JUDGE_TEST_PROJECT}")}"
judge_seal_graded_assets

swap_dir
restore_offline
build_tests
# A TRX is requested so the event stream can be corroborated against the test framework's own accounting.
# Without it a fabricated pass is undetectable.
run_tests --logger "trx;LogFileName=results.trx" --results-directory "${JUDGE_APP_DIR}/TestResults"
judge_assert_results

# Two independent corroborations, cheapest and sharpest first:
#   * the manifest catches results attributed to tests that do not exist, using a record taken before the
#     submission was in the container at all
#   * the TRX catches a pass count higher than the test framework itself counted
judge_verify_graded_assets
judge_verify_events_against_manifest
judge_verify_events_against_trx
