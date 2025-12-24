# Dynamic Table Email Template

This email template allows you to create emails with dynamic tables based on JSON data. Perfect for reports, dashboards, analytics, and any tabular data you need to send via email.

## Features

- ✨ **Dynamic table generation** from JSON data
- 📱 **Responsive design** that works on all email clients
- 🎨 **Professional styling** with clean, modern appearance
- 🔧 **Flexible column configuration** - define your own headers and data keys
- 👤 **Personalization** with recipient names
- 📧 **Easy integration** with existing email infrastructure

## API Endpoint

### POST `/api/send`

Send a dynamic table email by posting JSON data to the endpoint.

#### Request Body

```json
{
  "title": "Your Email Title",
  "description": "Optional description text",
  "recipientName": "Recipient Name (optional)",
  "to": "recipient@example.com",
  "subject": "Email Subject (optional, defaults to title)",
  "Columns": ["Column 1 Name", "Column 2 Name"],
  "Rows": [
    {
      "Data": {
        "Column 1 Name": "Value 1",
        "Column 2 Name": "Value 2"
      }
    },
    {
      "Data": {
        "Column 1 Name": "Value 3",
        "Column 2 Name": "Value 4"
      }
    }
  ]
}
```

#### Required Fields
- `title`: Email title/heading
- `Columns`: Array of column header strings
- `Rows`: Array of row objects, each containing a `Data` object with key-value pairs
- `to`: Recipient email address

#### Optional Fields
- `description`: Additional descriptive text below the title
- `recipientName`: Personalizes the greeting
- `subject`: Email subject line (defaults to `title` if not provided)

#### Data Structure
This structure matches the C# model:
```csharp
public class DataTable 
{
    public List<string> Columns { get; set; }
    public List<DataTableRow> Rows { get; set; }
}

public class DataTableRow
{
    public Dictionary<string, object?> Data { get; set; } = new();
}
```

## Usage Examples

### Example 1: Sales Report

```json
{
  "title": "Monthly Sales Report",
  "description": "Here's your monthly sales performance summary.",
  "recipientName": "Sales Manager",
  "to": "manager@company.com",
  "subject": "October 2025 Sales Report",
  "Columns": ["Product", "Revenue", "Growth"],
  "Rows": [
    {
      "Data": {
        "Product": "Product A",
        "Revenue": "$50,000",
        "Growth": "+15%"
      }
    },
    {
      "Data": {
        "Product": "Product B",
        "Revenue": "$32,000",
        "Growth": "+8%"
      }
    }
  ]
}
```

### Example 2: Team Performance

```json
{
  "title": "Team Performance Review",
  "recipientName": "HR Team",
  "to": "hr@company.com",
  "Columns": ["Employee", "Department", "Score", "Status"],
  "Rows": [
    {
      "Data": {
        "Employee": "John Doe",
        "Department": "Engineering",
        "Score": "95",
        "Status": "✅ Excellent"
      }
    },
    {
      "Data": {
        "Employee": "Jane Smith",
        "Department": "Marketing",
        "Score": "88",
        "Status": "✅ Good"
      }
    }
  ]
}
```

## Testing

### Using PowerShell (Windows)
```powershell
.\test-email.ps1
```

### Using Node.js
```bash
node test-email.js
```

### Using cURL
```bash
curl -X POST http://localhost:3000/api/send \
  -H "Content-Type: application/json" \
  -d @test-data.json
```

### Using Postman
1. Set method to `POST`
2. URL: `http://localhost:3000/api/send`
3. Headers: `Content-Type: application/json`
4. Body: Raw JSON with the data structure above

## Development

### File Structure
```
emails/
├── dynamic-table.tsx          # Email template component
├── src/pages/api/send.ts      # API endpoint
├── test-data.json            # Sample test data
├── test-email.js             # Node.js test script
├── test-email.ps1            # PowerShell test script
└── README-dynamic-table.md   # This documentation
```

### Local Development
1. Start the email preview server:
   ```bash
   npm run dev
   ```
2. The server runs on `http://localhost:3000`
3. Use the test scripts to send emails
4. Check your email inbox for the results

## Styling Customization

The email template uses inline styles for maximum email client compatibility. You can customize the appearance by modifying the style objects in `dynamic-table.tsx`:

- `main`: Overall email background
- `container`: Email content container
- `h1`: Title styling
- `text`: Body text styling
- `tableStyle`: Table container
- `headerCell`: Table header cells
- `dataCell`: Table data cells
- `footerText`: Footer styling

## Error Handling

The API returns appropriate HTTP status codes:
- `200`: Email sent successfully
- `400`: Missing required fields
- `405`: Method not allowed (only GET and POST supported)
- `500`: Internal server error

## Email Client Compatibility

This template is designed to work across major email clients including:
- Gmail
- Outlook (desktop and web)
- Apple Mail
- Yahoo Mail
- Thunderbird
- Mobile email clients

The table uses standard HTML `<table>` elements with inline CSS for maximum compatibility.