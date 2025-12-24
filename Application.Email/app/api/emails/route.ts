import { Resend } from "resend";
// import SubscribeEmail from "@/app/emails/subscribe";
import DynamicTableEmail from '../../../emails/dynamic-table';
import { NextResponse } from "next/server";

const resend = new Resend(process.env.RESEND_API_KEY);

// TypeScript interfaces to match your C# classes
interface DataTableRow {
  Data: { [key: string]: any };
}

interface ShareRequest {
  from: string;
  recipientName: string;
  title: string;
  description: string;
  messageContent: string;
  Columns?: string[];
  Rows?: DataTableRow[];
}

export async function POST(request: Request) {
//   const { name, email } = await request.json();
  const shareRequest: ShareRequest = await request.json();
  const { 
          from,
          recipientName,
          title, 
          description, 
          Columns, 
          Rows
        } = shareRequest; //= await request.json();

  // Validate required fields
  if (!recipientName || !title || !description) {
    return NextResponse.json({
      status: "ERROR",
      message: "Missing required fields: recipientName, title, or description"
    }, { status: 400 });
  }


  await resend.emails.send({

    from: from,
    to: recipientName,
    subject: title,
    react: await DynamicTableEmail({
        title, 
        description, 
        Columns: Columns || [], 
        Rows: Rows || [], 
        recipientName
        }),
  });

  return NextResponse.json({
    status: "OK",
  });
}




export async function GET() {
//   const { name, email } = await request.json();

  return NextResponse.json({
    status: "OK",
  });
}