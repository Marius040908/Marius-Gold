#!/usr/bin/env python3
"""Offline trade-plan Monte Carlo for 5-minute OHLCV CSV. Standard library only.

This is research, NOT a cTrader runtime component and cannot approve or place orders.
Input timestamps represent the local session clock selected by the user.
Never feed bars from the day being traded into the historical sample.
"""
import argparse
import csv
import json
import math
import random
from collections import defaultdict
from datetime import datetime, date, time, timedelta


def parse_clock(value):
    return datetime.strptime(value, "%H:%M").time()


def load_sessions(csv_path, before, start, end):
    sessions = defaultdict(list)
    with open(csv_path, newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        needed = {"timestamp", "open", "high", "low", "close"}
        if not needed.issubset(set(reader.fieldnames or [])):
            raise ValueError("CSV requires timestamp,open,high,low,close columns")
        for row in reader:
            stamp = datetime.fromisoformat(row["timestamp"].replace("Z", "+00:00"))
            if stamp.date() >= before or not (start <= stamp.time() < end):
                continue
            vals = tuple(float(row[k]) for k in ("open", "high", "low", "close"))
            o, h, l, c = vals
            if not all(math.isfinite(v) and v > 0 for v in vals):
                raise ValueError("invalid non-positive or non-finite OHLC")
            if h < max(o, c, l) or l > min(o, c, h):
                raise ValueError("OHLC relationships invalid")
            sessions[stamp.date()].append((stamp.time(), o, h, l, c))
    for d in sessions:
        sessions[d].sort(key=lambda r: r[0])
        if len({r[0] for r in sessions[d]}) != len(sessions[d]):
            raise ValueError("duplicate bars on date " + str(d))
    return dict(sessions)


def make_pool(sessions, start, end):
    """Bars are re-based to the PRECEDING close, preserving a historical opening gap."""
    by_clock = defaultdict(list)
    for day, rows in sessions.items():
        previous = None
        for clock, o, h, l, c in rows:
            if previous is not None:
                by_clock[clock].append((o / previous, h / previous, l / previous, c / previous))
            previous = c
    return dict(by_clock)


def evaluate_path(entry, stop, target, side, returns, rnd=None, cost_r=0.0):
    """Stop-first on ambiguous bars; never assumes a favorable intrabar fill."""
    direction = 1 if side == "long" else -1
    risk = abs(entry - stop)
    target_r = abs(target - entry) / risk
    price = entry
    for op, hi, lo, cl in returns:
        h, l = price * hi, price * lo
        stop_hit = (l <= stop) if direction == 1 else (h >= stop)
        target_hit = (h >= target) if direction == 1 else (l <= target)
        if stop_hit:
            return "stop", -1.0 - cost_r, price * cl
        if target_hit:
            return "target", target_r - cost_r, price * cl
        price *= cl
    return "neither", direction * (price - entry) / risk - cost_r, price


def run_analysis(sessions, entry, stop, target, side, entry_clock, close_clock,
                 replays=10000, seed=19, cost_r=0.0, min_days=20):
    if replays < 1 or replays > 100000:
        raise ValueError("replays must be 1..100000")
    if cost_r < 0 or not math.isfinite(cost_r):
        raise ValueError("roundtrip cost in R must be nonnegative")
    if side == "long" and not (stop < entry < target):
        raise ValueError("long requires stop < entry < target")
    if side == "short" and not (target < entry < stop):
        raise ValueError("short requires target < entry < stop")
    if not all(math.isfinite(x) and x > 0 for x in (entry, stop, target)):
        raise ValueError("invalid price")
    if len(sessions) < min_days:
        raise ValueError("only %d prior sessions; need %d" % (len(sessions), min_days))

    clock = entry_clock
    clocks = []
    while clock < close_clock:
        clocks.append(clock)
        clock = (datetime.combine(date(2000, 1, 1), clock) + timedelta(minutes=5)).time()
    if not clocks:
        raise ValueError("entry clock must precede exit clock")
    pool = make_pool(sessions, entry_clock, close_clock)
    available = [k for k in clocks if pool.get(k)]
    if len(available) < len(clocks) * .8:
        raise ValueError("too many missing 5-minute historical clock slots")
    rng = random.Random(seed)
    counts = {"target": 0, "stop": 0, "neither": 0}
    values, ending = [], []
    for _ in range(replays):
        path = [rng.choice(pool[k]) for k in available]
        outcome, result_r, final_price = evaluate_path(entry, stop, target, side, path, cost_r=cost_r)
        counts[outcome] += 1
        values.append(result_r)
        ending.append(final_price)
    # Clock-aligned historical replay, not a claim the exact strategy triggered every day.
    history = {"target": 0, "stop": 0, "neither": 0}
    history_r = []
    for day, bars in sessions.items():
        before = [r[4] for r in bars if r[0] < entry_clock]
        future = [r for r in bars if r[0] >= entry_clock and r[0] < close_clock]
        if not before or not future:
            continue
        historical_entry = before[-1]
        fraction_risk = abs(entry - stop) / entry
        historical_stop = historical_entry * (1 - fraction_risk) if side == "long" else historical_entry * (1 + fraction_risk)
        multiple = abs(target - entry) / abs(stop - entry)
        historical_target = historical_entry * (1 + fraction_risk * multiple) if side == "long" else historical_entry * (1 - fraction_risk * multiple)
        if historical_target <= 0:
            continue
        source = []
        preceding = historical_entry
        for _, o, h, l, c in future:
            source.append((o / preceding, h / preceding, l / preceding, c / preceding))
            preceding = c
        name, val, _ = evaluate_path(historical_entry, historical_stop, historical_target, side, source, cost_r=cost_r)
        history[name] += 1
        history_r.append(val)
    ending.sort()
    return {
        "kind": "historical-bar bootstrap, not a forecast",
        "seed": seed, "simulations": replays, "historical_sessions": len(sessions),
        "side": side, "entry": entry, "stop": stop, "target": target, "cost_r": cost_r,
        "probabilities_pct": {key: round(100 * val / replays, 2) for key, val in counts.items()},
        "mean_r": round(sum(values) / len(values), 4),
        "ending_price_median": round(ending[len(ending)//2], 5),
        "ending_price_p05": round(ending[int(.05 * (len(ending)-1))], 5),
        "ending_price_p95": round(ending[int(.95 * (len(ending)-1))], 5),
        "clock_aligned_history": {
            "count": len(history_r), "outcomes": history,
            "mean_r": round(sum(history_r) / len(history_r), 4) if history_r else None,
            "warning": "clock aligned, not a backtest of the cBot's EMA/RSI/ORB entry rules"
        },
        "limitations": ["5-minute OHLC paths", "stop first if both limits reached",
                        "past returns may not reflect future volatility",
                        "execution/slippage/gaps differ from broker fills",
                        "does not model cBot's full execution or funding"]
    }


def main():
    p = argparse.ArgumentParser(description="Offline 10k replay, no orders or APIs")
    p.add_argument("--csv", required=True, help="Local 5m OHLCV with session-local timestamp")
    p.add_argument("--entry", type=float, required=True)
    p.add_argument("--stop", type=float, required=True)
    p.add_argument("--target", type=float, required=True)
    p.add_argument("--side", choices=("long", "short"), default="long")
    p.add_argument("--at", default="10:15", help="session-local first future bar HH:MM")
    p.add_argument("--session-start", default="09:30")
    p.add_argument("--close", default="15:55")
    p.add_argument("--before", default=date.today().isoformat(), help="exclude this date and later, YYYY-MM-DD")
    p.add_argument("--replays", type=int, default=10000)
    p.add_argument("--seed", type=int, default=19)
    p.add_argument("--cost-r", type=float, default=0.1, help="estimated total friction in initial R, not exact")
    args = p.parse_args()
    sessions = load_sessions(args.csv, date.fromisoformat(args.before),
                             parse_clock(args.session_start), parse_clock(args.close))
    report = run_analysis(sessions, args.entry, args.stop, args.target,
                          args.side, parse_clock(args.at), parse_clock(args.close),
                          args.replays, args.seed, args.cost_r)
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
