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

interface DatasetSharedEmailProps {
  recipientName?: string;
  datasetName: string;
  sharedByName: string;
  accessLevel: string;
  datasetUrl?: string;
  message?: string;
}

const email_signature = process.env.EMAIL_SIGNATURE || 'The Data Team';

const getAccessColor = (accessLevel?: string) => {
  switch ((accessLevel || '').toLowerCase()) {
    case 'administrator':
      return '#b91c1c';
    case 'editor':
      return '#c2410c';
    case 'viewer':
      return '#047857';
    default:
      return '#4f46e5';
  }
};

const capabilitiesFor = (accessLevel?: string): string[] => {
  const level = (accessLevel || '').toLowerCase();
  const caps: string[] = [];
  if (level === 'administrator') {
    caps.push('Full administrative access', 'Share with other users');
  }
  if (level === 'administrator' || level === 'editor') {
    caps.push('Edit dataset structure', 'Add and modify tables');
  }
  caps.push('View dataset contents', 'Query data', 'Generate reports');
  return caps;
};

export const DatasetSharedEmail: React.FC<Readonly<DatasetSharedEmailProps>> = ({
  recipientName,
  datasetName,
  sharedByName,
  accessLevel,
  datasetUrl,
  message,
}) => {
  const accessColor = getAccessColor(accessLevel);
  const capabilities = capabilitiesFor(accessLevel);

  return (
    <Html>
      <Head />
      <Preview>{`${sharedByName} shared the dataset "${datasetName}" with you`}</Preview>
      <Body style={main}>
        <Container style={container}>
          <Section style={{ marginBottom: '8px' }}>
            <span style={{ ...badge, backgroundColor: accessColor }}>
              {(accessLevel || 'Access').toUpperCase()}
            </span>
          </Section>

          <Heading style={heading}>A dataset was shared with you</Heading>

          <Text style={paragraph}>
            {message ||
              `${sharedByName} has shared the dataset "${datasetName}" with you. Your access level is ${accessLevel}.`}
          </Text>

          <Section style={card}>
            <Text style={cardLabel}>Dataset</Text>
            <Text style={cardValue}>{datasetName}</Text>
          </Section>

          <Section style={{ ...card, marginTop: '12px' }}>
            <Text style={cardLabel}>What you can do</Text>
            {capabilities.map((cap) => (
              <Text key={cap} style={capability}>
                • {cap}
              </Text>
            ))}
          </Section>

          {datasetUrl ? (
            <Section style={{ textAlign: 'center', margin: '28px 0' }}>
              <Button style={{ ...button, backgroundColor: accessColor }} href={datasetUrl}>
                Open dataset
              </Button>
            </Section>
          ) : null}

          <Text style={muted}>
            You are receiving this because someone shared a dataset with your account.
          </Text>
          <Text style={signature}>{email_signature}</Text>
        </Container>
      </Body>
    </Html>
  );
};

export default DatasetSharedEmail;

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

const capability: React.CSSProperties = {
  fontSize: '14px',
  lineHeight: '22px',
  color: '#374151',
  margin: '2px 0',
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
