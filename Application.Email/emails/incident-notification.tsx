import {
  Body,
  Button,
  Container,
  Head,
  Heading,
  Html,
  Preview,
  Section,
  Text,
} from '@react-email/components';

interface IncidentNotificationEmailProps {
  entityName: string;
  incidentTitle: string;
  severity?: string;
  message?: string;
  statusUrl: string;
}

const email_signature = process.env.EMAIL_SIGNATURE || 'Status Monitoring';

const getSeverityColor = (severity?: string) => {
  switch ((severity || '').toLowerCase()) {
    case 'critical':
      return '#b91c1c';
    case 'high':
      return '#c2410c';
    case 'medium':
      return '#b45309';
    case 'low':
      return '#047857';
    default:
      return '#6b7280';
  }
};

export const IncidentNotificationEmail: React.FC<Readonly<IncidentNotificationEmailProps>> = ({
  entityName,
  incidentTitle,
  severity,
  message,
  statusUrl,
}) => {
  const severityColor = getSeverityColor(severity);

  return (
    <Html>
      <Head />
      <Preview>{`Issue detected: ${entityName}`}</Preview>
      <Body style={main}>
        <Container style={container}>
          <Section style={{ marginBottom: '8px' }}>
            <span style={{ ...badge, backgroundColor: severityColor }}>
              {(severity || 'Issue').toUpperCase()}
            </span>
          </Section>

          <Heading style={heading}>Issue detected: {entityName}</Heading>

          <Text style={paragraph}>
            {message ||
              `An issue has been detected with ${entityName}. The team has been notified and is working on it.`}
          </Text>

          <Section style={card}>
            <Text style={cardLabel}>Incident</Text>
            <Text style={cardValue}>{incidentTitle}</Text>
          </Section>

          <Section style={{ textAlign: 'center', margin: '28px 0' }}>
            <Button style={{ ...button, backgroundColor: severityColor }} href={statusUrl}>
              View incident status
            </Button>
          </Section>

          <Text style={muted}>
            You are receiving this because you are part of the audience for this service or one it
            depends on.
          </Text>
          <Text style={signature}>{email_signature}</Text>
        </Container>
      </Body>
    </Html>
  );
};

export default IncidentNotificationEmail;

const main: React.CSSProperties = {
  backgroundColor: '#f3f4f6',
  fontFamily:
    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
  padding: '24px 0',
};

const container: React.CSSProperties = {
  backgroundColor: '#ffffff',
  borderRadius: '12px',
  padding: '32px',
  maxWidth: '560px',
  margin: '0 auto',
  border: '1px solid #e5e7eb',
};

const badge: React.CSSProperties = {
  display: 'inline-block',
  color: '#ffffff',
  fontSize: '11px',
  fontWeight: 700,
  letterSpacing: '0.05em',
  padding: '4px 10px',
  borderRadius: '9999px',
};

const heading: React.CSSProperties = {
  fontSize: '22px',
  fontWeight: 700,
  color: '#111827',
  margin: '8px 0 12px',
};

const paragraph: React.CSSProperties = {
  fontSize: '15px',
  lineHeight: '24px',
  color: '#374151',
  margin: '0 0 20px',
};

const card: React.CSSProperties = {
  backgroundColor: '#f9fafb',
  border: '1px solid #e5e7eb',
  borderRadius: '8px',
  padding: '16px',
};

const cardLabel: React.CSSProperties = {
  fontSize: '11px',
  textTransform: 'uppercase',
  letterSpacing: '0.05em',
  color: '#6b7280',
  margin: '0 0 4px',
};

const cardValue: React.CSSProperties = {
  fontSize: '16px',
  fontWeight: 600,
  color: '#111827',
  margin: 0,
};

const button: React.CSSProperties = {
  color: '#ffffff',
  fontSize: '14px',
  fontWeight: 600,
  borderRadius: '8px',
  padding: '12px 22px',
  textDecoration: 'none',
};

const muted: React.CSSProperties = {
  fontSize: '12px',
  lineHeight: '18px',
  color: '#9ca3af',
  margin: '0 0 4px',
};

const signature: React.CSSProperties = {
  fontSize: '13px',
  color: '#6b7280',
  margin: '12px 0 0',
};
