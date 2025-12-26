import {
  Body,
  Container,
  Head,
  Heading,
  Html,
  Preview,
  Text,
} from '@react-email/components';

interface DataTableRow {
  Data?: { [key: string]: any };
  [key: string]: any;
}

type TrendDirection = 'up' | 'down' | 'flat';

interface MetricCard {
  label: string;
  value: string;
  changeLabel?: string;
  changeDirection?: TrendDirection;
  helperText?: string;
}

interface SchemeKpiCard {
  schemeName: string;
  totalSales: string;
  deltaLabel?: string;
  changeDirection?: TrendDirection;
  stores?: number;
  transactions?: number;
}

interface SalesSnapshotEmailProps {
  recipientName?: string;
  title: string;
  description?: string;
  lastUpdatedLabel?: string;
  totalKpis?: MetricCard[];
  schemeKpis?: SchemeKpiCard[];
  Columns?: string[];
  Rows?: DataTableRow[];
}

const email_signature = process.env.EMAIL_SIGNATURE || 'Sales Insights';

const getTrendColor = (direction?: TrendDirection) => {
  switch (direction) {
    case 'up':
      return '#047857';
    case 'down':
      return '#b91c1c';
    default:
      return '#6b7280';
  }
};

const getTrendIcon = (direction?: TrendDirection) => {
  switch (direction) {
    case 'up':
      return '▲';
    case 'down':
      return '▼';
    default:
      return '◆';
  }
};

const resolveCellValue = (row: DataTableRow, column: string) => {
  if (row?.Data && Object.prototype.hasOwnProperty.call(row.Data, column)) {
    return row.Data[column];
  }

  if (Object.prototype.hasOwnProperty.call(row, column)) {
    return row[column];
  }

  return '';
};

export const SalesSnapshotEmail: React.FC<Readonly<SalesSnapshotEmailProps>> = ({
  recipientName,
  title,
  description,
  lastUpdatedLabel,
  totalKpis = [],
  schemeKpis = [],
  Columns = [],
  Rows = [],
}) => {
  const previewText = description ?? 'Live sales snapshot overview';
  const hasTableData = Columns.length > 0 && Rows.length > 0;

  return (
    <Html>
      <Head />
      <Preview>{previewText}</Preview>
      <Body style={bodyStyle}>
        <Container style={containerStyle}>
          <Heading style={titleStyle}>{title}</Heading>
          {recipientName && (
            <Text style={paragraphStyle}>Hello {recipientName},</Text>
          )}
          {description && <Text style={paragraphStyle}>{description}</Text>}
          <Text style={metaLineStyle}>
            Last updated: {lastUpdatedLabel ?? 'Not provided'} • View: Stores
          </Text>

          {totalKpis.length > 0 && (
            <div style={sectionWrapper}>
              <Text style={sectionHeading}>Overall Performance</Text>
              <div style={kpiGrid}>
                {totalKpis.map((kpi, index) => (
                  <div key={index} style={kpiCard}>
                    <div style={kpiLabel}>{kpi.label}</div>
                    <div style={kpiValue}>{kpi.value}</div>
                    {kpi.changeLabel && (
                      <div
                        style={{
                          ...trendText,
                          color: getTrendColor(kpi.changeDirection),
                        }}
                      >
                        {getTrendIcon(kpi.changeDirection)} {kpi.changeLabel}
                      </div>
                    )}
                    {kpi.helperText && (
                      <div style={helperText}>{kpi.helperText}</div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {schemeKpis.length > 0 && (
            <div style={sectionWrapper}>
              <Text style={sectionHeading}>Performance by Scheme</Text>
              <div style={schemeGrid}>
                {schemeKpis.map((scheme, index) => (
                  <div key={index} style={schemeCard}>
                    <div style={schemeHeader}>{scheme.schemeName}</div>
                    <div style={schemeValue}>{scheme.totalSales}</div>
                    {scheme.deltaLabel && (
                      <div
                        style={{
                          ...trendText,
                          color: getTrendColor(scheme.changeDirection),
                        }}
                      >
                        {getTrendIcon(scheme.changeDirection)} {scheme.deltaLabel}
                      </div>
                    )}
                    {(typeof scheme.stores === 'number' || typeof scheme.transactions === 'number') && (
                      <div style={helperText}>
                        {typeof scheme.stores === 'number' ? `${scheme.stores} stores` : ''}
                        {typeof scheme.stores === 'number' && typeof scheme.transactions === 'number' ? ' / ' : ''}
                        {typeof scheme.transactions === 'number' ? `${scheme.transactions.toLocaleString()} transactions` : ''}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          <div style={sectionWrapper}>
            <div style={tableHeaderRow}>
              <Text style={sectionHeading}>Store Performance Details</Text>
              <Text style={tableHint}>Snapshot sorted by store metrics</Text>
            </div>
            {hasTableData ? (
              <table style={tableStyle}>
                <thead>
                  <tr>
                    {Columns.map((column, index) => (
                      <th key={index} style={tableHeaderCell}>
                        {column}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {Rows.map((row, rowIndex) => (
                    <tr key={rowIndex}>
                      {Columns.map((column, colIndex) => {
                        const cellValue = resolveCellValue(row, column);
                        return (
                          <td key={colIndex} style={tableCell}>
                            {String(cellValue ?? '')}
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <Text style={emptyStateText}>
                No store performance data was provided for this snapshot.
              </Text>
            )}
          </div>

          <Text style={footerTextStyle}>Best regards,</Text>
          <Text style={footerBrand}>{email_signature}</Text>
        </Container>
      </Body>
    </Html>
  );
};

export default SalesSnapshotEmail;

const bodyStyle = {
  backgroundColor: '#f1f5f9',
  margin: '0 auto',
  padding: '32px 0',
  fontFamily:
    "-apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif",
};

const containerStyle = {
  backgroundColor: '#ffffff',
  borderRadius: '16px',
  padding: '40px',
  maxWidth: '640px',
  margin: '0 auto',
  boxShadow: '0 20px 50px rgba(15, 23, 42, 0.08)',
};

const titleStyle = {
  color: '#0f172a',
  fontSize: '26px',
  fontWeight: 700,
  margin: '0 0 16px',
  textAlign: 'center' as const,
};

const paragraphStyle = {
  color: '#475569',
  fontSize: '15px',
  lineHeight: '24px',
  margin: '0 0 12px',
};

const metaLineStyle = {
  color: '#94a3b8',
  fontSize: '13px',
  margin: '4px 0 24px',
  textAlign: 'center' as const,
};

const sectionWrapper = {
  marginBottom: '32px',
};

const sectionHeading = {
  color: '#1f2937',
  fontSize: '16px',
  fontWeight: 600,
  margin: '0 0 16px',
};

const kpiGrid = {
  display: 'flex',
  flexWrap: 'wrap' as const,
  gap: '16px',
  margin: '-8px',
};

const kpiCard = {
  background: '#f8fafc',
  borderRadius: '12px',
  border: '1px solid #e2e8f0',
  padding: '16px',
  flex: '1 1 180px',
  margin: '8px',
};

const kpiLabel = {
  fontSize: '13px',
  fontWeight: 600,
  color: '#64748b',
  marginBottom: '12px',
};

const kpiValue = {
  fontSize: '28px',
  fontWeight: 700,
  color: '#0f172a',
  marginBottom: '8px',
};

const trendText = {
  fontSize: '13px',
  fontWeight: 600,
};

const helperText = {
  fontSize: '12px',
  color: '#94a3b8',
  marginTop: '6px',
};

const schemeGrid = {
  display: 'flex',
  flexWrap: 'wrap' as const,
  gap: '16px',
  margin: '-8px',
};

const schemeCard = {
  background: '#111827',
  borderRadius: '12px',
  padding: '18px',
  color: 'white',
  flex: '1 1 200px',
  margin: '8px',
};

const schemeHeader = {
  fontSize: '13px',
  letterSpacing: '0.04em',
  textTransform: 'uppercase' as const,
  color: '#cbd5f5',
  marginBottom: '10px',
};

const schemeValue = {
  fontSize: '24px',
  fontWeight: 700,
  marginBottom: '8px',
};

const tableHeaderRow = {
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'baseline',
  gap: '12px',
};

const tableHint = {
  color: '#94a3b8',
  fontSize: '13px',
  margin: 0,
};

const tableStyle = {
  width: '100%',
  borderCollapse: 'collapse' as const,
  border: '1px solid #e2e8f0',
  borderRadius: '12px',
  overflow: 'hidden',
};

const tableHeaderCell = {
  backgroundColor: '#f8fafc',
  color: '#1f2937',
  fontSize: '13px',
  fontWeight: 600,
  textAlign: 'left' as const,
  padding: '12px 16px',
  borderBottom: '1px solid #e2e8f0',
  borderRight: '1px solid #e2e8f0',
};

const tableCell = {
  padding: '12px 16px',
  fontSize: '13px',
  color: '#475569',
  borderBottom: '1px solid #e2e8f0',
  borderRight: '1px solid #e2e8f0',
};

const emptyStateText = {
  color: '#94a3b8',
  fontSize: '14px',
  margin: '0',
};

const footerTextStyle = {
  color: '#475569',
  fontSize: '14px',
  margin: '32px 0 4px',
};

const footerBrand = {
  color: '#0f172a',
  fontSize: '16px',
  fontWeight: 600,
  margin: '0',
};
