"use client";
import Image from "next/image";
import { useEffect, useState } from "react";
import { toDataURL } from "qrcode";

export function JoinQr({ url }: { url: string }) {
  const [image, setImage] = useState("");
  useEffect(() => {
    let active = true;
    void toDataURL(url, { width: 160, margin: 2, errorCorrectionLevel: "M" })
      .then((data) => {
        if (active) setImage(data);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [url]);
  return image ? (
    <div className="join-qr">
      <Image
        src={image}
        width={110}
        height={110}
        alt="QR code to join this lecture"
        unoptimized
      />
      <span>
        Scan, join,
        <br />
        follow along.
      </span>
    </div>
  ) : null;
}
