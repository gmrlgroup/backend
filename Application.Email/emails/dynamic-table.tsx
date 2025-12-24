import {
  Body,
  Container,
  Head,
  Heading,
  Html,
  Preview,
  Text,
} from '@react-email/components';

// interface DataTableRow {
//   Data?: { [key: string]: any };
//   [key: string]: any; // Allow direct properties on the row object
// }

// interface DynamicTableEmailProps {
//   title: string;
//   description?: string;
//   Columns: string[];
//   Rows: DataTableRow[];
//   recipientName?: string;
// }

// TypeScript interfaces to match your C# classes
interface DataTableRow {
  Data: { [key: string]: any };
}

interface ShareRequest {
  recipientName: string;
  title: string;
  description: string;
  messageContent: string;
  Columns?: string[];
  Rows?: DataTableRow[];
}


// Add the missing interface for component props
interface DynamicTableEmailProps {
  recipientName?: string;
  title: string;
  description?: string;
  // messageContent?: string;
  Columns: string[];
  Rows: DataTableRow[];
}

export const DynamicTableEmail: React.FC<Readonly<DynamicTableEmailProps>> = ({
  recipientName,
  title,
  description,
  Columns = [],
  Rows = []
}) => {
  // Validate that we have the required data
  if (!Columns || Columns.length === 0) {
    return (
      <Html>
        <Head />
        <Preview>Error: No columns defined</Preview>
        <Body style={main}>
          <Container style={container}>
            <Heading style={h1}>Email Configuration Error</Heading>
            <Text style={text}>
              No table columns were defined for this email.
            </Text>
          </Container>
        </Body>
      </Html>
    );
  }

  return (
    <Html>
      <Head />
      <Preview>{title}</Preview>
      <Body style={main}>
        <Container style={container}>
          <Heading style={h1}>{title}</Heading>
          {recipientName && (
            <Text style={text}>Hello {recipientName},</Text>
          )}
          {description && (
            <Text style={text}>{description}</Text>
          )}
          
          <table style={tableStyle}>
            <thead>
              <tr>
                {Columns.map((column: string, index: number) => (
                  <th key={index} style={headerCell}>
                    {column}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {Rows.map((row: DataTableRow, rowIndex: number) => (
                <tr key={rowIndex}>
                  {Columns.map((column: string, colIndex: number) => {
                    // Access the data from the Data property to match C# structure
                    // const cellValue = row.Data?.[column] ?? '';
                    // const cellValue = row.Data?.[column] ?? '';
                    // Try multiple ways to access the data
                      // let cellValue = '';
                      
                      // // Method 1: Check if row has Data property (C# structure)
                      // if (row.Data && typeof row.Data === 'object') {
                      //   cellValue = row.Data[column];
                      // }
                      // // Method 2: Check if the value is directly on the row
                      // else if (row[column] !== undefined) {
                      //   cellValue = row[column];
                      // }
                      // // Method 3: Handle case where Data might be lowercase
                      // else if (row.data && typeof row.data === 'object') {
                      //   cellValue = row.data[column];
                      // }

                      let cellValue = row.Data[column];

                      console.log(`Column: ${column}, Value: ${cellValue}`);
                    return (
                      <td key={colIndex} style={dataCell}>
                        {String(cellValue)}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
          
          <Text style={footerText}>
            Best regards,<br />
          </Text>
        </Container>
      </Body>
    </Html>
  );
};

export default DynamicTableEmail;

const main = {
  backgroundColor: '#f6f9fc',
  margin: '0 auto',
  fontFamily:
    "-apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif",
};

const container = {
  backgroundColor: '#ffffff',
  margin: 'auto',
  padding: '40px 20px',
  borderRadius: '8px',
  maxWidth: '600px',
  marginTop: '40px',
  marginBottom: '40px',
  boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)',
};

const h1 = {
  color: '#333333',
  fontSize: '24px',
  fontWeight: '600',
  lineHeight: '32px',
  margin: '0 0 20px',
  textAlign: 'center' as const,
};

const text = {
  color: '#555555',
  fontSize: '16px',
  lineHeight: '24px',
  margin: '0 0 20px',
};

const tableStyle = {
  width: '100%',
  borderCollapse: 'collapse' as const,
  margin: '20px 0',
  border: '1px solid #e1e8ed',
  borderRadius: '6px',
  overflow: 'hidden',
};

const headerCell = {
  backgroundColor: '#f8f9fa',
  padding: '12px 16px',
  border: '1px solid #e1e8ed',
  color: '#333333',
  fontSize: '14px',
  fontWeight: '600',
  textAlign: 'left' as const,
};

const dataCell = {
  padding: '12px 16px',
  border: '1px solid #e1e8ed',
  color: '#555555',
  fontSize: '14px',
  textAlign: 'left' as const,
};

const footerText = {
  color: '#888888',
  fontSize: '14px',
  lineHeight: '20px',
  margin: '30px 0 0',
  textAlign: 'center' as const,
};