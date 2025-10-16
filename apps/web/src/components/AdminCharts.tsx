import { PieChart, Pie, Cell, BarChart, Bar, LineChart, Line, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, CartesianGrid } from 'recharts';

type EndpointChartProps = {
  endpointCounts: Record<string, number>;
};

type LatencyChartProps = {
  requests: Array<{ latencyMs: number; endpoint: string; createdAt: string }>;
};

type TimeSeriesChartProps = {
  requests: Array<{ createdAt: string; status: string }>;
};

type FeedbackChartProps = {
  feedbackCounts: Record<string, number>;
};

const ENDPOINT_COLORS: Record<string, string> = {
  qa: "#1a73e8",
  explain: "#f9ab00",
  setup: "#a142f4",
  chess: "#34a853",
};

const COLORS = ["#1a73e8", "#f9ab00", "#a142f4", "#34a853", "#ea4335"];

export function EndpointDistributionChart({ endpointCounts }: EndpointChartProps) {
  const data = Object.entries(endpointCounts).map(([name, value]) => ({
    name,
    value,
    color: ENDPOINT_COLORS[name] || "#64748b"
  }));

  if (data.length === 0) {
    return (
      <div style={{ padding: 48, textAlign: "center", color: "#64748b" }}>
        No endpoint data available
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={300}>
      <PieChart>
        <Pie
          data={data}
          dataKey="value"
          nameKey="name"
          cx="50%"
          cy="50%"
          outerRadius={100}
          label={({ name, value }) => `${name}: ${value}`}
        >
          {data.map((entry, index) => (
            <Cell key={`cell-${index}`} fill={entry.color} />
          ))}
        </Pie>
        <Tooltip />
        <Legend />
      </PieChart>
    </ResponsiveContainer>
  );
}

export function LatencyDistributionChart({ requests }: LatencyChartProps) {
  if (requests.length === 0) {
    return (
      <div style={{ padding: 48, textAlign: "center", color: "#64748b" }}>
        No latency data available
      </div>
    );
  }

  // Group latency into bins
  const bins = [0, 100, 200, 300, 400, 500, 1000];
  const binCounts = bins.slice(0, -1).map((bin, i) => {
    const nextBin = bins[i + 1];
    const count = requests.filter(r => r.latencyMs >= bin && r.latencyMs < nextBin).length;
    return {
      range: `${bin}-${nextBin}ms`,
      count,
    };
  });

  return (
    <ResponsiveContainer width="100%" height={300}>
      <BarChart data={binCounts}>
        <CartesianGrid strokeDasharray="3 3" />
        <XAxis dataKey="range" />
        <YAxis />
        <Tooltip />
        <Legend />
        <Bar dataKey="count" fill="#1a73e8" name="Requests" />
      </BarChart>
    </ResponsiveContainer>
  );
}

export function RequestsTimeSeriesChart({ requests }: TimeSeriesChartProps) {
  if (requests.length === 0) {
    return (
      <div style={{ padding: 48, textAlign: "center", color: "#64748b" }}>
        No time series data available
      </div>
    );
  }

  // Group by hour
  const grouped = requests.reduce((acc, req) => {
    const date = new Date(req.createdAt);
    const hour = new Date(date.getFullYear(), date.getMonth(), date.getDate(), date.getHours()).toISOString();

    if (!acc[hour]) {
      acc[hour] = { time: hour, success: 0, error: 0, total: 0 };
    }

    acc[hour].total++;
    if (req.status === "Success") {
      acc[hour].success++;
    } else {
      acc[hour].error++;
    }

    return acc;
  }, {} as Record<string, { time: string; success: number; error: number; total: number }>);

  const data = Object.values(grouped)
    .sort((a, b) => new Date(a.time).getTime() - new Date(b.time).getTime())
    .map(item => ({
      ...item,
      time: new Date(item.time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    }));

  return (
    <ResponsiveContainer width="100%" height={300}>
      <LineChart data={data}>
        <CartesianGrid strokeDasharray="3 3" />
        <XAxis dataKey="time" />
        <YAxis />
        <Tooltip />
        <Legend />
        <Line type="monotone" dataKey="total" stroke="#1a73e8" name="Total Requests" strokeWidth={2} />
        <Line type="monotone" dataKey="success" stroke="#0f9d58" name="Successful" strokeWidth={2} />
        <Line type="monotone" dataKey="error" stroke="#d93025" name="Errors" strokeWidth={2} />
      </LineChart>
    </ResponsiveContainer>
  );
}

export function FeedbackChart({ feedbackCounts }: FeedbackChartProps) {
  const data = Object.entries(feedbackCounts).map(([name, value], index) => ({
    name: name === "helpful" ? "Helpful" : "Not Helpful",
    value,
    color: name === "helpful" ? "#34a853" : "#ea4335"
  }));

  if (data.length === 0 || data.every(d => d.value === 0)) {
    return (
      <div style={{ padding: 48, textAlign: "center", color: "#64748b" }}>
        No feedback data available
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={300}>
      <BarChart data={data}>
        <CartesianGrid strokeDasharray="3 3" />
        <XAxis dataKey="name" />
        <YAxis />
        <Tooltip />
        <Legend />
        <Bar dataKey="value" name="Feedback Count">
          {data.map((entry, index) => (
            <Cell key={`cell-${index}`} fill={entry.color} />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
