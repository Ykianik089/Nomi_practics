import csv
import matplotlib.pyplot as plt

rows = list(csv.DictReader(open("docs/latency_samples.csv")))

accepted = [r for r in rows if r["event"] == "Accepted" and r["rtt_ms"]]

x      = list(range(len(accepted)))
rtt    = [float(r["rtt_ms"])    for r in accepted]
srtt   = [float(r["srtt_ms"])   for r in accepted]
jitter = [float(r["jitter_ms"]) for r in accepted]

plt.figure(figsize=(10, 5))
plt.plot(x, rtt,    label="RTT")
plt.plot(x, srtt,   label="SRTT")
plt.plot(x, jitter, label="Jitter")
plt.xlabel("PONG sample"); plt.ylabel("ms")
plt.legend(); plt.grid(True); plt.tight_layout()
plt.savefig("docs/latency_rtt.png")

loss = [r for r in rows if r["event"] == "LOSS"]
print("Loss events:", len(loss))